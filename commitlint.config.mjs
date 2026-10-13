/** @type {import('@commitlint/types').UserConfig} */
export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'scope-enum': [2, 'always', ['frontend', 'backend', 'ci', 'deps', 'deps-dev', 'repo']],
  },
};
