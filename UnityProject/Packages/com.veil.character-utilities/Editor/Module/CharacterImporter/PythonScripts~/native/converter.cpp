#include <fbxsdk.h>
#include <windows.h>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <map>
#include <regex>
#include <set>
#include <stdexcept>
#include <string>
#include <vector>
#include <cmath>
#include <iostream>

using Numbers = std::map<std::string, std::vector<double>>;
static void require(bool value, const std::string& message) { if (!value) throw std::runtime_error(message); }
static std::string utf8(const wchar_t* s) {
    int n = WideCharToMultiByte(CP_UTF8, 0, s, -1, nullptr, 0, nullptr, nullptr);
    std::string out(n, '\0'); WideCharToMultiByte(CP_UTF8, 0, s, -1, out.data(), n, nullptr, nullptr);
    out.pop_back(); return out;
}
static std::string quote(const std::string& s) {
    std::string out = "\"";
    for (unsigned char c : s) {
        if (c == '"' || c == '\\') { out += '\\'; out += c; }
        else if (c < 32) { char b[7]; sprintf_s(b, "\\u%04x", c); out += b; }
        else out += c;
    }
    return out + "\"";
}
static std::string renamed(const std::string& name) {
    auto s = std::regex_replace(name, std::regex("^mixamorig[0-9]*:"), "");
    if (s == "Spine") return "Spine_0";
    auto p = s.find_last_not_of("0123456789");
    if (p != std::string::npos && p + 1 < s.size() && s[p] != '_') s.insert(p + 1, "_");
    return s;
}
static bool identity(const FbxAMatrix& m) {
    for (int i=0;i<4;i++) for(int j=0;j<4;j++) if(std::abs(m.Get(i,j)-(i==j?1.0:0.0))>1e-9) return false;
    return true;
}
static bool animated(FbxNode* node, FbxScene* scene) {
    for(int s=0;s<scene->GetSrcObjectCount<FbxAnimStack>();s++) {
        auto stack=scene->GetSrcObject<FbxAnimStack>(s);
        for(int l=0;l<stack->GetMemberCount<FbxAnimLayer>();l++) {
            auto layer=stack->GetMember<FbxAnimLayer>(l);
            for(auto p=node->GetFirstProperty();p.IsValid();p=node->GetNextProperty(p))
                if(p.GetCurveNode(layer,false)) return true;
        }
    }
    return false;
}
static FbxScene* load(FbxManager* m, const std::string& path) {
    auto scene=FbxScene::Create(m,"Animation"); auto importer=FbxImporter::Create(m,"");
    require(importer->Initialize(path.c_str(),-1,m->GetIOSettings()),importer->GetStatus().GetErrorString());
    require(importer->Import(scene),importer->GetStatus().GetErrorString()); importer->Destroy(); return scene;
}
static void appendMatrix(std::vector<double>& values,const FbxAMatrix& m) {
    for(int i=0;i<4;i++)for(int j=0;j<4;j++)values.push_back(m.Get(i,j));
}
static void snapshotNode(FbxNode* node, const std::string& parent, FbxScene* scene, Numbers& data) {
    auto path=parent+"/"+node->GetName();
    auto& state=data["node:"+path]; state.push_back(node->GetChildCount());
    state.push_back(node->GetSkeleton()?node->GetSkeleton()->GetSkeletonType():-1);
    appendMatrix(state,node->EvaluateLocalTransform(FBXSDK_TIME_ZERO));
    appendMatrix(state,node->EvaluateGlobalTransform(FBXSDK_TIME_ZERO));
    for(int s=0;s<scene->GetSrcObjectCount<FbxAnimStack>();s++) {
        auto stack=scene->GetSrcObject<FbxAnimStack>(s);
        for(int l=0;l<stack->GetMemberCount<FbxAnimLayer>();l++) {
            auto layer=stack->GetMember<FbxAnimLayer>(l);
            for(auto p=node->GetFirstProperty();p.IsValid();p=node->GetNextProperty(p)) {
                auto cn=p.GetCurveNode(layer,false); if(!cn)continue;
                for(unsigned ch=0;ch<cn->GetChannelsCount();ch++)for(unsigned ci=0;ci<cn->GetCurveCount(ch);ci++) {
                    auto curve=cn->GetCurve(ch,ci);if(!curve)continue;
                    auto key="curve:"+path+":"+std::to_string(s)+":"+std::to_string(l)+":"+p.GetHierarchicalName().Buffer()+":"+std::to_string(ch)+":"+std::to_string(ci);
                    auto& values=data[key];
                    for(int k=0;k<curve->KeyGetCount();k++) {
                        values.push_back((double)curve->KeyGetTime(k).Get());values.push_back(curve->KeyGetValue(k));
                        values.push_back(curve->KeyGetInterpolation(k));
                        if(curve->KeyGetInterpolation(k)==FbxAnimCurveDef::eInterpolationCubic) {
                            values.push_back(curve->KeyGetTangentMode(k,true));
                            values.push_back(curve->KeyGetLeftDerivative(k));values.push_back(curve->KeyGetRightDerivative(k));
                            values.push_back(curve->KeyGetLeftTangentWeight(k));values.push_back(curve->KeyGetRightTangentWeight(k));
                        }
                    }
                }
            }
        }
    }
    for(int i=0;i<node->GetChildCount();i++)snapshotNode(node->GetChild(i),path,scene,data);
}
static Numbers snapshot(FbxScene* scene) {
    Numbers data;
    data["units"]={scene->GetGlobalSettings().GetSystemUnit().GetScaleFactor()};
    int sign=0; auto axis=scene->GetGlobalSettings().GetAxisSystem();
    data["axes"]={(double)axis.GetUpVector(sign),(double)sign,(double)axis.GetCoorSystem()};
    auto front=axis.GetFrontVector(sign);data["axes"].push_back(front);data["axes"].push_back(sign);
    for(int s=0;s<scene->GetSrcObjectCount<FbxAnimStack>();s++) {
        auto stack=scene->GetSrcObject<FbxAnimStack>(s); auto span=stack->GetLocalTimeSpan();
        data["stack:"+std::to_string(s)+":"+stack->GetName()]={(double)span.GetStart().Get(),(double)span.GetStop().Get(),(double)stack->GetMemberCount<FbxAnimLayer>()};
    }
    for(int i=0;i<scene->GetRootNode()->GetChildCount();i++)snapshotNode(scene->GetRootNode()->GetChild(i),"",scene,data);
    return data;
}
static void writeSnapshot(const Numbers& values, const std::string& path) {
    std::ofstream out(std::filesystem::u8path(path));require((bool)out,"Cannot write snapshot");out<<std::setprecision(17)<<"{";bool first=true;
    for(const auto& entry:values){if(!first)out<<",";first=false;out<<quote(entry.first)<<":[";for(size_t i=0;i<entry.second.size();i++){if(i)out<<",";require(std::isfinite(entry.second[i]),"Non-finite FBX data");out<<entry.second[i];}out<<"]";}out<<"}";
}
template<class T> static void destroyObjects(FbxScene* scene){while(scene->GetSrcObjectCount<T>())scene->GetSrcObject<T>(0)->Destroy();}
static void destroyNodeTree(FbxNode* node) {
    while(node->GetChildCount())destroyNodeTree(node->GetChild(0));
    // Recursive object destruction follows skin links and can destroy shared bones.
    // Traverse only the node hierarchy; delete geometry/deformers separately below.
    node->Destroy(false);
}

int wmain(int argc,wchar_t** argv) {
    FbxManager* manager=nullptr;
    try {
        require(argc==6,"Usage: converter input output report before.json after.json");
        auto input=utf8(argv[1]), output=utf8(argv[2]);
        manager=FbxManager::Create();require(manager!=nullptr,"Cannot create FBX manager");
        auto settings=FbxIOSettings::Create(manager,IOSROOT);manager->SetIOSettings(settings);
        settings->SetBoolProp(IMP_FBX_EXTRACT_EMBEDDED_DATA,false);
        auto scene=load(manager,input);
        require(scene->GetSrcObjectCount<FbxConstraint>()==0,"Constraints require baking before conversion");
        std::vector<FbxNode*> bones;std::map<std::string,FbxNode*> names;std::vector<std::pair<std::string,std::string>> mappings;
        for(int i=0;i<scene->GetNodeCount();i++) {
            auto node=scene->GetNode(i);if(!node->GetSkeleton())continue;
            auto name=renamed(node->GetName());
            require(names.emplace(name,node).second,"Bone name collision: "+name);
            bones.push_back(node);mappings.push_back({node->GetName(),name});
        }
        require(!bones.empty(),"No skeleton bones found");require(names.count("Hips"),"A unique Hips bone is required");
        auto hips=names.at("Hips");auto sceneRoot=scene->GetRootNode();
        auto root=names.count("Bone_Root")?names.at("Bone_Root"):nullptr;
        if(root){require(hips->GetParent()==root,"Existing Bone_Root must directly parent Hips");require(identity(root->EvaluateLocalTransform()),"Existing Bone_Root must have identity transform");require(!animated(root,scene),"Animated Bone_Root is not supported by this Mixamo converter");}
        auto top=root?root:hips;
        for(auto parent=top->GetParent();parent&&parent!=sceneRoot;parent=parent->GetParent()) {
            require(!parent->GetSkeleton(),"Unexpected bone ancestor above Hips");
            require(identity(parent->EvaluateLocalTransform()),"Non-identity model containers need explicit baking");
            require(!animated(parent,scene),"Animated model containers need explicit baking");
        }
        for(auto bone:bones) {
            if(bone==root||bone==hips)continue;
            require(bone->GetParent()&&bone->GetParent()->GetSkeleton(),"Non-bone intermediary requires explicit conversion");
            auto parent=bone;while(parent&&parent!=hips)parent=parent->GetParent();
            require(parent==hips,"Multiple skeletons are not supported in one input");
        }
        // SDK renames preserve node connections and animation curve bindings.
        for(size_t i=0;i<bones.size();i++)bones[i]->SetName(mappings[i].second.c_str());
        sceneRoot->AddChild(top); // Only identity, unanimated containers may be removed.
        bool addedRoot=root==nullptr;
        if(addedRoot){root=FbxNode::Create(scene,"Bone_Root");auto sk=FbxSkeleton::Create(scene,"Bone_Root");sk->SetSkeletonType(FbxSkeleton::eRoot);root->SetNodeAttribute(sk);sceneRoot->AddChild(root);root->AddChild(hips);}
        // Skeleton-only output: remove non-bone branches and all geometry/media.
        std::set<FbxNode*> keep(bones.begin(),bones.end());keep.insert(root);
        std::vector<FbxNode*> remove;
        for(int i=0;i<scene->GetNodeCount();i++){auto n=scene->GetNode(i);if(n!=sceneRoot&&!keep.count(n)&&n->GetParent()&&(n->GetParent()==sceneRoot||keep.count(n->GetParent())))remove.push_back(n);}
        for(auto n:remove)destroyNodeTree(n);
        while(scene->GetPoseCount()){auto pose=scene->GetPose(0);scene->RemovePose(pose);pose->Destroy();}
        destroyObjects<FbxGeometry>(scene);destroyObjects<FbxDeformer>(scene);destroyObjects<FbxSurfaceMaterial>(scene);destroyObjects<FbxTexture>(scene);destroyObjects<FbxVideo>(scene);
        require(sceneRoot->GetChildCount()==1&&sceneRoot->GetChild(0)==root,"Unexpected output hierarchy");
        auto before=snapshot(scene);writeSnapshot(before,utf8(argv[4]));
        settings->SetBoolProp(EXP_FBX_MATERIAL,false);settings->SetBoolProp(EXP_FBX_TEXTURE,false);settings->SetBoolProp(EXP_FBX_EMBEDDED,false);
        auto exporter=FbxExporter::Create(manager,"");require(exporter->Initialize(output.c_str(),-1,settings),exporter->GetStatus().GetErrorString());require(exporter->Export(scene),exporter->GetStatus().GetErrorString());exporter->Destroy();
        auto check=load(manager,output);require(check->GetGeometryCount()==0,"Output still contains geometry");require(check->GetMaterialCount()==0,"Output still contains materials");
        int resultBones=0;for(int i=0;i<check->GetNodeCount();i++){auto n=check->GetNode(i);if(n==check->GetRootNode())continue;require(n->GetSkeleton()!=nullptr,"Output includes a non-bone node");resultBones++;}
        require(resultBones==(int)bones.size()+(addedRoot?1:0),"Output bone count changed");
        writeSnapshot(snapshot(check),utf8(argv[5]));
        std::ofstream report(std::filesystem::u8path(utf8(argv[3])));report<<"{\"bone_count\":"<<resultBones<<",\"added_root\":"<<(addedRoot?"true":"false")<<",\"animation_stacks\":"<<check->GetSrcObjectCount<FbxAnimStack>()<<",\"mapping\":[";
        for(size_t i=0;i<mappings.size();i++){if(i)report<<",";report<<"{\"source\":"<<quote(mappings[i].first)<<",\"target\":"<<quote(mappings[i].second)<<"}";}report<<"]}";
        manager->Destroy();return 0;
    } catch(const std::exception& e) {std::cerr<<e.what()<<"\n";if(manager)manager->Destroy();return 1;}
}
