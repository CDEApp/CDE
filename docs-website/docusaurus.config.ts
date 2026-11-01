import type * as Preset from '@docusaurus/preset-classic';
import type {Config} from '@docusaurus/types';
import {themes as prismThemes} from 'prism-react-renderer';

const organizationName = 'CDEApp';
const projectName = 'CDE';
const repoUrl = `https://github.com/${organizationName}/${projectName}`;

const config: Config = {
  title: 'cde',
  tagline: 'Catalog Directory Entries: scan, search and find duplicate files across drives',
  favicon: 'img/favicon.png',

  // Published to GitHub Pages at https://cdeapp.github.io/CDE/
  url: `https://${organizationName.toLowerCase()}.github.io`,
  baseUrl: `/${projectName}/`,
  organizationName,
  projectName,
  trailingSlash: false,

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          // Docs-only mode: the docs are served from the site root.
          routeBasePath: '/',
          sidebarPath: './sidebars.ts',
          editUrl: `${repoUrl}/tree/develop/docs-website/`,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      {
        // Offline search: the index is built at compile time and served as static assets,
        // so it works on GitHub Pages with no search backend.
        hashed: true,
        // Docs-only mode serves the docs from '/', and the indexer defaults to '/docs'.
        docsRouteBasePath: '/',
        indexBlog: false,
        highlightSearchTermsOnTargetPage: true,
      },
    ],
  ],

  themeConfig: {
    image: 'img/logo.png',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'cde',
      logo: {
        alt: 'cde logo',
        src: 'img/logo.png',
      },
      items: [
        {type: 'docSidebar', sidebarId: 'docsSidebar', position: 'left', label: 'User guide'},
        {to: '/commands', label: 'Command reference', position: 'left'},
        {href: `${repoUrl}/releases`, label: 'Download', position: 'right'},
        {href: repoUrl, label: 'GitHub', position: 'right'},
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {label: 'Introduction', to: '/'},
            {label: 'Installation', to: '/installation'},
            {label: 'Quick start', to: '/guide/quick-start'},
            {label: 'Command reference', to: '/commands'},
          ],
        },
        {
          title: 'More',
          items: [
            {label: 'GitHub', href: repoUrl},
            {label: 'Releases', href: `${repoUrl}/releases`},
            {label: 'Issues', href: `${repoUrl}/issues`},
            {label: 'Changelog', href: `${repoUrl}/blob/develop/Changelog.md`},
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} cde contributors.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['bash', 'powershell', 'batch', 'json'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
