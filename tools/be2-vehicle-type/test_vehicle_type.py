import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from types import SimpleNamespace

HERE=Path(__file__).parent
spec=importlib.util.spec_from_file_location('be2_vehicle_type',HERE/'vehicle_type.py')
vt=importlib.util.module_from_spec(spec)
spec.loader.exec_module(vt)


class BasicChecks(unittest.TestCase):
    def test_reject_bad_ids_and_category(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            (root/'x.jpg').write_bytes(b'x')
            m=root/'rows.jsonl'
            m.write_text(json.dumps({'id':'plate 59A12345','image':'x.jpg','vehicle_type':'car','split':'train'})+'\n')
            with self.assertRaisesRegex(vt.DatasetError,'anonymous'): vt.read_manifest(m,root)
            m.write_text(json.dumps({'id':'safe-1','image':'x.jpg','vehicle_type':'Car','split':'train'})+'\n')
            with self.assertRaisesRegex(vt.DatasetError,'vehicle_type'): vt.read_manifest(m,root)

    def test_no_absolute_or_parent_image_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            (root/'x.png').write_bytes(b'data')
            for name in ('../x.png','/tmp/x.png','..\\x.png'):
                with self.subTest(name=name):
                    with self.assertRaises(vt.DatasetError): vt.safe_location(root,name)

    def test_symlink_escape_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            ext=root.parent/'vehicle-type-test-outside.png'
            ext.write_bytes(b'data')
            try:
                (root/'link.png').symlink_to(ext)
                with self.assertRaisesRegex(vt.DatasetError,'escapes'): vt.safe_location(root,'link.png')
            finally:
                ext.unlink(missing_ok=True)

    def test_duplicate_id_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            (root/'x.jpg').write_bytes(b'x')
            obj={'id':'a','image':'x.jpg','vehicle_type':'car','split':'train'}
            f=root/'x.jsonl';f.write_text(json.dumps(obj)+'\n'+json.dumps(obj)+'\n')
            with self.assertRaisesRegex(vt.DatasetError,'duplicate sample id'): vt.read_manifest(f,root)

    def test_cross_split_minimum_samples(self):
        rows=[{'vehicle_type':'car','split':'train'},{'vehicle_type':'motorbike','split':'train'}]
        with self.assertRaisesRegex(vt.DatasetError,'not enough'):vt.check_training_distribution(rows)

    def test_no_false_gates_with_insufficient_validation(self):
        self.assertIsNone(vt.choose_threshold([0,1,0,1],[0,1,0,1],[.8,.9,.9,.8],.9,5))

    def test_threshold_uses_coverage_and_accuracy(self):
        self.assertEqual(vt.choose_threshold([0,1,0,1],[0,1,0,0],[.8,.9,.95,.55],1.0,2),.6)

    def test_metrics_count_abstentions_as_errors(self):
        result=vt.metrics([0,1,1],[0,1,1],[.95,.5,.6],.8,['day','day','night'])
        self.assertEqual(result['samples'],3)
        self.assertEqual(result['accepted'],1)
        self.assertAlmostEqual(result['accuracy_all_including_abstentions'],.3333,places=4)
        self.assertEqual(result['groups_min_5_samples'],{})

    def test_model_invalid_rejected(self):
        try:import numpy as np
        except ImportError:self.skipTest('numpy not installed')
        with tempfile.TemporaryDirectory() as tmp:
            f=Path(tmp)/'invalid.npz'
            np.savez(f,version=1,labels=['car','motorbike'])
            with self.assertRaisesRegex(vt.DatasetError,'incomplete'):vt.load_model(f)

    def test_binary_softmax_normalization(self):
        try:import numpy as np
        except ImportError:self.skipTest('numpy not installed')
        arr=vt.softmax(np.array([[-2],[0],[2]],dtype=float))
        self.assertEqual(arr.shape,(3,2))
        np.testing.assert_allclose(arr.sum(axis=1),1)
        self.assertLess(arr[0,1],arr[2,1])

    def test_multiclass_softmax_normalization(self):
        try:import numpy as np
        except ImportError:self.skipTest('numpy not installed')
        arr=vt.softmax(np.array([[0,3,-4],[0,0,0]],dtype=float))
        self.assertEqual(arr.shape,(2,3))
        self.assertEqual(int(arr[0].argmax()),1)
        np.testing.assert_allclose(arr.sum(axis=1),1)


class RealOfflineTraining(unittest.TestCase):
    def test_train_predict_and_no_leakage_using_synthetic_drawings(self):
        try:
            from PIL import Image, ImageDraw
            import numpy as np
            import sklearn
        except ImportError:
            self.skipTest('Pillow/numpy/scikit-learn not available')
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); data=root/'images'; data.mkdir()
            manifest=root/'data.jsonl'; predict_manifest=root/'predict.jsonl'
            samples=[];inputs=[]
            for split,n in (('train',10),('validation',5),('test',5)):
                for label in ('car','motorbike'):
                    for j in range(n):
                        img=Image.new('RGB',(120,120),'white');draw=ImageDraw.Draw(img)
                        noise=(j*7 + (3 if split=='validation' else 13 if split=='test' else 1))
                        if label=='car':
                            draw.rectangle((10, 35+noise%6, 110, 75+noise%6), fill=(20+noise%100,10,10))
                            draw.rectangle((20, 25, 90, 35+noise%6), fill=(20,20,20))
                        else:
                            draw.ellipse((36+noise%6, 5, 64+noise%6, 105),fill=(5,5,5))
                            draw.line((10,60+noise%5,90,60+noise%5),fill=(30,30,30),width=4)
                        draw.rectangle((j,105,j+3,110),fill=(noise%200,50,40))
                        name=f'{split}-{label}-{j}.png';img.save(data/name)
                        uid=f'{split}-{label}-{j}'
                        samples.append({'id':uid,'image':name,'vehicle_type':label,'split':split,'group':'day'})
                        if split=='test':inputs.append({'id':uid,'image':name})
            manifest.write_text(''.join(json.dumps(x)+'\n' for x in samples))
            predict_manifest.write_text(''.join(json.dumps(x)+'\n' for x in inputs))
            a=SimpleNamespace(dataset_root=str(data),manifest=str(manifest),report=str(root/'audit.json'),min_per_split=2)
            self.assertEqual(vt.run_audit(a),0)
            self.assertEqual(json.loads((root/'audit.json').read_text())['split_counts']['test'],10)
            args=SimpleNamespace(dataset_root=str(data),manifest=str(manifest),model=str(root/'baseline.npz'),
                report=str(root/'aggregate.json'),min_per_split=2,min_accepted_accuracy=.70,min_validation_accepted=3)
            ret=vt.run_train(args)
            report=json.loads((root/'aggregate.json').read_text())
            self.assertEqual(ret,0)
            self.assertEqual(report['status'],'OFFLINE_CANDIDATE_ONLY')
            self.assertEqual(report['test']['samples'],10)
            self.assertNotIn(str(data),json.dumps(report))
            self.assertNotIn('test-car-0',json.dumps(report))
            self.assertGreater(report['test']['accuracy_all_including_abstentions'],0.5)
            pa=SimpleNamespace(dataset_root=str(data),manifest=str(predict_manifest),model=str(root/'baseline.npz'),output=str(root/'private_predictions.jsonl'))
            self.assertEqual(vt.run_predict(pa),0)
            outputs=[json.loads(line) for line in (root/'private_predictions.jsonl').read_text().splitlines()]
            self.assertEqual(len(outputs),10)
            self.assertTrue(all('image' not in x for x in outputs))
            self.assertEqual({x['id'] for x in outputs},{x['id'] for x in inputs})
            # Impossible acceptance policy must never export a usable model.
            fail_args=SimpleNamespace(dataset_root=str(data),manifest=str(manifest),model=str(root/'not_ready.npz'),
                report=str(root/'not_ready_report.json'),min_per_split=2,min_accepted_accuracy=1.0,min_validation_accepted=100)
            self.assertEqual(vt.run_train(fail_args),3)
            self.assertFalse((root/'not_ready.npz').exists())
            self.assertFalse(json.loads((root/'not_ready_report.json').read_text())['validation_threshold_found'])
            # Train/test leak is rejected even when the image is renamed to a different file.
            copied=data/'leak.png'; copied.write_bytes((data/'train-car-0.png').read_bytes())
            samples.append({'id':'leak-extra','image':'leak.png','vehicle_type':'car','split':'test','group':'day'})
            manifest.write_text(''.join(json.dumps(x)+'\n' for x in samples))
            with self.assertRaisesRegex(vt.DatasetError,'duplicate image pixels'):
                vt.extract_images(vt.read_manifest(manifest,data))


if __name__=='__main__':unittest.main()
