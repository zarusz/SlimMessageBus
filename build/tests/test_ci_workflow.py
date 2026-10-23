"""Regression checks for fork isolation and the manually approved PR source."""

import json
from pathlib import Path
import subprocess
import unittest

import yaml


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = yaml.safe_load((ROOT / '.github/workflows/build.yml').read_text(encoding='utf-8'))
JOBS = WORKFLOW['jobs']
SHA = 'a' * 40
REPOSITORY = 'zarusz/SlimMessageBus'


def run_script(script, event='workflow_dispatch', ref='refs/heads/master', number='', reviewed='', pr=None, merge_base=SHA):
    """Execute the actual workflow JS against a mocked read-only GitHub API."""
    data = {
        'context': {
            'eventName': event, 'ref': ref, 'sha': SHA,
            'repo': {'owner': 'zarusz', 'repo': 'SlimMessageBus'},
            'payload': {'repository': {'full_name': REPOSITORY}},
        },
        'pr': pr or {
            'state': 'open',
            'head': {'sha': SHA, 'repo': {'full_name': 'contributor/SlimMessageBus'}},
            'base': {'ref': 'master', 'repo': {'full_name': REPOSITORY}},
        },
        'number': number, 'reviewed': reviewed, 'mergeBase': merge_base,
    }
    harness = """
const data = JSON.parse(process.argv[1]);
process.env.PR_NUMBER = data.number;
process.env.REVIEWED_SHA = data.reviewed;
const outputs = {};
let reads = 0;
const github = {rest: {
  pulls: {get: async () => { reads++; return {data: data.pr}; }},
  repos: {compareCommitsWithBasehead: async () => ({data: {merge_base_commit: {sha: data.mergeBase}}})}
}};
const summary = {addHeading() {return this;}, addCodeBlock() {return this;}, async write() {}};
const core = {setOutput(key, value) {outputs[key] = value;}, summary};
const AsyncFunction = Object.getPrototypeOf(async function() {}).constructor;
(async () => {
  try {
    await new AsyncFunction('context', 'github', 'core', SCRIPT)(data.context, github, core);
    console.log(JSON.stringify({outputs, reads}));
  } catch (error) {
    console.log(JSON.stringify({error: error.message, outputs, reads}));
  }
})();
""".replace('SCRIPT', json.dumps(script))
    result = subprocess.run(['node', '-e', harness, json.dumps(data)], check=True, capture_output=True, text=True)
    return json.loads(result.stdout)


class WorkflowIsolationTests(unittest.TestCase):
    def test_forks_use_unprivileged_pr_event(self):
        # PyYAML's YAML 1.1 loader interprets the key "on" as True.
        triggers = WORKFLOW.get('on', WORKFLOW.get(True))
        self.assertIn('pull_request', triggers)
        self.assertNotIn('pull_request_target', triggers)
        self.assertEqual(WORKFLOW['permissions'], {'contents': 'read'})

    def test_local_jobs_never_receive_secrets_or_a_protected_environment(self):
        for name in ('source', 'build', 'integration-tests', 'report', 'ci'):
            with self.subTest(job=name):
                job = JOBS[name]
                self.assertNotIn('secrets.', json.dumps(job))
                self.assertNotIn('environment', job)
                for step in job.get('steps', []):
                    if step.get('uses', '').startswith('actions/checkout@'):
                        self.assertFalse(step['with']['persist-credentials'])

    def test_privileged_jobs_are_excluded_from_pr_events(self):
        cloud = JOBS['cloud-tests']
        self.assertNotIn('pull_request', cloud['if'])
        self.assertIn("github.event_name == 'push'", cloud['if'])
        self.assertIn("github.event_name == 'workflow_dispatch'", cloud['if'])
        self.assertIn("'integration-tests'", cloud['environment'])
        self.assertIn("github.event_name == 'push'", JOBS['sonar']['if'])
        # The stale-head check must run before any checkout in the approved job.
        self.assertEqual(cloud['steps'][0]['name'], 'Recheck reviewed PR after approval')

    def test_local_outbox_filters_exclude_cloud_cases(self):
        matrix = JOBS['integration-tests']['strategy']['matrix']['include']
        for leg in matrix:
            self.assertNotIn('Azure', leg['filter'])
            if leg['transport'].startswith('Outbox.') and 'DbContext' in leg['transport']:
                self.assertTrue('Outbox.Kafka' in leg['filter'] or 'Outbox.RabbitMQ' in leg['filter'])


class ManualSourceTests(unittest.TestCase):
    script = JOBS['source']['steps'][0]['with']['script']

    def test_regular_pr_keeps_merge_commit_without_api_lookup(self):
        result = run_script(self.script, event='pull_request', ref='refs/pull/484/merge')
        self.assertEqual(result, {'outputs': {'repository': REPOSITORY, 'sha': SHA}, 'reads': 0})

    def test_manual_branch_run_uses_trigger_commit(self):
        self.assertEqual(run_script(self.script)['outputs'], {'repository': REPOSITORY, 'sha': SHA})

    def test_manual_pr_checks_out_exact_reviewed_fork_commit(self):
        result = run_script(self.script, number='484', reviewed=SHA)
        self.assertEqual(result['outputs'], {'repository': 'contributor/SlimMessageBus', 'sha': SHA})
        self.assertEqual(result['reads'], 1)

    def test_rejects_untrusted_dispatch_branch_or_invalid_inputs(self):
        for args in (
            {'ref': 'refs/heads/feature/ci'},
            {'number': '484'},
            {'number': '484; echo injected', 'reviewed': SHA},
            {'number': '484', 'reviewed': 'master'},
            {'reviewed': SHA},
        ):
            with self.subTest(args=args):
                result = run_script(self.script, **args)
                self.assertIn('error', result)
                self.assertEqual(result['outputs'], {})

    def test_rejects_changed_closed_or_unavailable_pr(self):
        for state, head_sha, head_repo, base_repo, base_ref in (
            ('open', 'b' * 40, {'full_name': 'fork/repo'}, REPOSITORY, 'master'),
            ('closed', SHA, {'full_name': 'fork/repo'}, REPOSITORY, 'master'),
            ('open', SHA, None, REPOSITORY, 'master'),
            ('open', SHA, {'full_name': 'fork/repo'}, 'other/repo', 'master'),
            ('open', SHA, {'full_name': 'fork/repo'}, REPOSITORY, 'feature/untrusted'),
        ):
            pr = {'state': state, 'head': {'sha': head_sha, 'repo': head_repo},
                  'base': {'repo': {'full_name': base_repo}, 'ref': base_ref}}
            with self.subTest(pr=pr):
                result = run_script(self.script, number='484', reviewed=SHA, pr=pr)
                self.assertIn('error', result)
                self.assertEqual(result['outputs'], {})

    def test_rechecks_head_after_environment_approval(self):
        script = JOBS['cloud-tests']['steps'][0]['with']['script']
        self.assertNotIn('error', run_script(script, number='484', reviewed=SHA))
        self.assertIn('error', run_script(script, number='484', reviewed='b' * 40))

    def test_manual_pr_must_include_trusted_branch_ci_setup(self):
        result = run_script(self.script, number='484', reviewed=SHA, merge_base='b' * 40)
        self.assertIn('Update the PR', result['error'])
        self.assertEqual(result['outputs'], {})


if __name__ == '__main__':
    unittest.main()
