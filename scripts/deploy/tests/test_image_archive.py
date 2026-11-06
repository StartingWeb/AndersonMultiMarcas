import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest import mock
from test_release import release


class ImageArchiveTests(unittest.TestCase):
    def fixture(self, directory, oci_identity=False):
        commit = 'a' * 40
        configuration = {'architecture': 'amd64', 'os': 'linux',
                         'config': {'User': 'app', 'Labels': {release.REVISION_LABEL: commit}},
                         'rootfs': {'type': 'layers', 'diff_ids': ['sha256:' + 'b' * 64]}}
        config = json.dumps(configuration).encode()
        config_id = 'sha256:' + hashlib.sha256(config).hexdigest()
        image_manifest = json.dumps({'schemaVersion': 2, 'config': {'digest': config_id}}).encode()
        oci_id = 'sha256:' + hashlib.sha256(image_manifest).hexdigest()
        tag = 'andersonmultimarcas-unified:' + commit
        files = {'manifest.json': json.dumps([{'Config': 'blobs/sha256/' + config_id[7:],
                                              'RepoTags': [tag], 'Layers': []}]).encode(),
                 'blobs/sha256/' + config_id[7:]: config,
                 'blobs/sha256/' + oci_id[7:]: image_manifest,
                 'index.json': json.dumps({'manifests': [{'digest': oci_id}]}).encode()}
        path = Path(directory) / 'image.tar.gz'
        with tarfile.open(path, 'w:gz') as archive:
            for name, value in files.items():
                member = tarfile.TarInfo(name)
                member.size = len(value)
                archive.addfile(member, io.BytesIO(value))
        manifest = {'imageId': oci_id if oci_identity else config_id, 'imageTag': tag, 'commit': commit}
        image = {'Id': oci_id, 'Architecture': 'amd64', 'Os': 'linux',
                 'Config': configuration['config'], 'RootFS': {'Layers': configuration['rootfs']['diff_ids']}}
        return path, manifest, configuration, image

    def test_classic_config_digest_and_oci_digest_both_bind_to_saved_configuration(self):
        for use_oci in [False, True]:
            with self.subTest(oci=use_oci), tempfile.TemporaryDirectory() as directory:
                path, manifest, configuration, image = self.fixture(directory, use_oci)
                self.assertEqual(configuration, release.archive_configuration(path, manifest))

    def test_loaded_oci_id_is_used_only_after_matching_configuration_and_rootfs(self):
        with tempfile.TemporaryDirectory() as directory:
            path, manifest, configuration, image = self.fixture(directory)
            engine = release.Docker()
            with mock.patch.object(engine, 'run', side_effect=['loaded', json.dumps([image])]):
                self.assertEqual(image['Id'], engine.check_image(manifest, path))
            self.assertNotEqual(manifest['imageId'], image['Id'])

    def test_changed_runtime_configuration_or_layers_cannot_pass_using_a_matching_label(self):
        for section in ['Config', 'RootFS']:
            with self.subTest(section=section), tempfile.TemporaryDirectory() as directory:
                path, manifest, configuration, image = self.fixture(directory)
                image[section] = {**image[section], **({'User': 'root'} if section == 'Config' else {'Layers': []})}
                engine = release.Docker()
                with mock.patch.object(engine, 'run', side_effect=['loaded', json.dumps([image])]):
                    with self.assertRaises(release.DeploymentError):
                        engine.check_image(manifest, path)

    def test_wrong_archive_tag_or_identity_is_rejected_before_docker_load(self):
        for field, value in [('imageTag', 'unrelated:latest'), ('imageId', 'sha256:' + 'f' * 64)]:
            with self.subTest(field=field), tempfile.TemporaryDirectory() as directory:
                path, manifest, configuration, image = self.fixture(directory)
                manifest[field] = value
                engine = release.Docker()
                with mock.patch.object(engine, 'run') as run:
                    with self.assertRaises(release.DeploymentError):
                        engine.check_image(manifest, path)
                    run.assert_not_called()


if __name__ == '__main__':
    unittest.main()
