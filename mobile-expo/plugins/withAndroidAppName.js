// Nom affiché sous l'icône Android (le nom Expo reste « BeninConnect », sans accent, pour la cible Xcode).
const { withStringsXml, AndroidConfig } = require('expo/config-plugins');

module.exports = function withAndroidAppName(config, { name }) {
  return withStringsXml(config, (c) => {
    c.modResults = AndroidConfig.Strings.setStringItem(
      [{ $: { name: 'app_name', translatable: 'false' }, _: name }],
      c.modResults,
    );
    return c;
  });
};
