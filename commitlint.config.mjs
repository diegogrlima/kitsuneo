/** @type {import('@commitlint/types').UserConfig} */
export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'scope-enum': [2, 'always', ['frontend', 'backend', 'ci', 'deps', 'deps-dev', 'repo']],
  },
  // Dependabot's NuGet updater ignores the commit-message settings and writes "chore: Bump ..."
  ignores: [(message) => /^chore(\(deps(-dev)?\))?: Bump /.test(message)],
};
