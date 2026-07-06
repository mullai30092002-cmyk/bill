import * as configJson from '../../../public/staticwebapp.config.json';

type StaticWebAppConfig = {
  navigationFallback: {
    rewrite: string;
  };
};

const config = ((configJson as { default?: StaticWebAppConfig }).default ?? configJson) as StaticWebAppConfig;

describe('SPA routing configuration', () => {
  it('staticwebapp.config.json should exist', () => {
    expect(config).toBeDefined();
  });

  it('rewrites deep links to index.html', () => {
    expect(config.navigationFallback.rewrite).toBe('/index.html');
  });
});
