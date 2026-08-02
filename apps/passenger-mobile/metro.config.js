const { getDefaultConfig } = require('expo/metro-config');
const { FileStore } = require('metro-cache');
const path = require('path');

const projectRoot = __dirname;
const config = getDefaultConfig(projectRoot);

// File-linked shared packages live outside this app. Resolve their peer imports
// from the app so Metro bundles the same React and native module instances.
config.resolver.nodeModulesPaths = [path.resolve(projectRoot, 'node_modules')];

// Passenger and Driver intentionally use different Worklets versions. Keep
// transformed shared-package output isolated so one app cannot reuse the
// other app's Babel-plugin version from Metro's global cache.
config.cacheStores = [
  new FileStore({ root: path.resolve(projectRoot, '.expo', 'metro-cache') }),
];

module.exports = config;
