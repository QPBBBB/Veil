"""Run with the bundled Python; pass --sample path.fbx for native integration tests."""
import argparse
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("converter", Path(__file__).resolve().parents[1] / "convert.py")
converter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(converter)
SAMPLE = None
MODEL = None


class VerificationTests(unittest.TestCase):
    def test_round_trip_tolerance_and_corruption(self):
        with tempfile.TemporaryDirectory() as folder:
            a, b = Path(folder) / "a.json", Path(folder) / "b.json"
            a.write_text(json.dumps({"node:/Bone_Root/Hips": [1, 0, 99.754], "curve:hips": [0, 1, 2]}))
            b.write_text(json.dumps({"node:/Bone_Root/Hips": [1, 1e-14, 99.754], "curve:hips": [0, 1, 2]}))
            converter.verify_snapshots(a, b)
            b.write_text(json.dumps({"node:/Bone_Root/Hips": [1, 0, 99.754], "curve:hips": [0, 5, 2]}))
            with self.assertRaises(RuntimeError):
                converter.verify_snapshots(a, b)
            b.write_text(json.dumps({"node:/WrongRoot/Hips": [1, 0, 99.754], "curve:hips": [0, 1, 2]}))
            with self.assertRaises(RuntimeError):
                converter.verify_snapshots(a, b)

    def test_existing_output_is_preserved(self):
        with tempfile.TemporaryDirectory() as folder:
            source, output = Path(folder) / "source.fbx", Path(folder) / "output.fbx"
            source.write_bytes(b"source")
            output.write_bytes(b"do not replace")
            with self.assertRaises(RuntimeError):
                converter.main([str(source), "--output", str(output), "--no-import"])
            self.assertEqual(output.read_bytes(), b"do not replace")

    def test_missing_input_does_not_create_output(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / "output.fbx"
            with self.assertRaises(RuntimeError):
                converter.main([str(Path(folder) / "missing.fbx"), "--output", str(output), "--no-import"])
            self.assertFalse(output.exists())

    def test_real_animation_conversion_and_idempotence(self):
        if SAMPLE is None:
            self.skipTest("Pass --sample for the native SDK integration test")
        original_hash = converter.sha(SAMPLE)
        with tempfile.TemporaryDirectory(prefix="骨骼测试 空格-") as folder:
            output = Path(folder) / "转换结果.fbx"
            log = io.StringIO()
            with contextlib.redirect_stdout(log):
                converter.main([str(SAMPLE), "--output", str(output), "--no-import"])
            report = json.loads(log.getvalue()[log.getvalue().index("{"):])
            self.assertGreater(report["verification"]["bone_curves"], 0)
            self.assertGreater(report["bone_count"], 0)
            self.assertTrue(output.exists())
            second = Path(folder) / "再次转换.fbx"
            log = io.StringIO()
            with contextlib.redirect_stdout(log):
                converter.main([str(output), "--output", str(second), "--no-import"])
            repeated = json.loads(log.getvalue()[log.getvalue().index("{"):])
            self.assertFalse(repeated["added_root"])
            self.assertEqual(report["bone_count"], repeated["bone_count"])
            self.assertTrue(all(row["source"] == row["target"] for row in repeated["mapping"]))
        self.assertEqual(converter.sha(SAMPLE), original_hash)

    def test_mesh_removal_preserves_shared_bones_and_in_place_meta(self):
        if MODEL is None:
            self.skipTest("Pass --model for the skinned-model removal regression test")
        original_hash = converter.sha(MODEL)
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / "skeleton.fbx"
            with contextlib.redirect_stdout(io.StringIO()):
                converter.main([str(MODEL), "--output", str(output), "--no-import"])
            meta = Path(str(output) + ".meta")
            meta.write_text("guid: regression-test-sentinel")
            log = io.StringIO()
            with contextlib.redirect_stdout(log):
                converter.main([str(output), "--in-place", "--no-import"])
            report = json.loads(log.getvalue()[log.getvalue().index("{"):])
            self.assertEqual(meta.read_text(), "guid: regression-test-sentinel")
            self.assertTrue((Path(report["backup"]) / "skeleton.fbx").exists())
            self.assertGreater(report["bone_count"], 0)
        self.assertEqual(converter.sha(MODEL), original_hash)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--sample", type=Path)
    parser.add_argument("--model", type=Path)
    args, remaining = parser.parse_known_args()
    SAMPLE = args.sample.resolve() if args.sample else None
    MODEL = args.model.resolve() if args.model else None
    unittest.main(argv=[__file__] + remaining)
