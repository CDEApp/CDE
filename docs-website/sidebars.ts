import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    {
      type: 'category',
      label: 'Start here',
      collapsed: false,
      items: ['intro', 'installation', 'guide/quick-start'],
    },
    {
      type: 'category',
      label: 'User guide',
      collapsed: false,
      items: [
        'guide/catalog-files',
        'guide/searching',
        'guide/finding-duplicates',
        'guide/applications',
        'guide/linux',
      ],
    },
    {
      type: 'category',
      label: 'Command reference',
      collapsed: false,
      link: {type: 'doc', id: 'commands/index'},
      items: [
        'commands/scan',
        'commands/find',
        'commands/everything',
        'commands/hash',
        'commands/dupes',
        'commands/migrate',
        'commands/other',
      ],
    },
    {
      type: 'category',
      label: 'Reference',
      collapsed: false,
      items: ['reference/value-formats', 'reference/configuration'],
    },
  ],
};

export default sidebars;
