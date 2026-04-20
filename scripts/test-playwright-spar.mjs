const playwrightModulePath = process.env.PLAYWRIGHT_CORE_PATH ?? 'playwright';
const { chromium } = await import(playwrightModulePath);

const url = 'https://www.spar.at/suche?q=tomate';

const browser = await chromium.launch({
  headless: true,
});

try {
  const page = await browser.newPage({
    viewport: { width: 1440, height: 900 },
    locale: 'de-AT',
    userAgent:
      'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36',
  });

  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(5000);

  const title = await page.title();
  const content = await page.content();
  const challengeDetected =
    content.includes('Just a moment...') ||
    content.includes('Enable JavaScript and cookies to continue') ||
    content.includes('__cf_chl_opt');

  const productLinks = await page.$$eval('a[href]', (links) =>
    links
      .map((link) => link.getAttribute('href') || '')
      .filter((href) => href.includes('/produkt') || href.includes('/produktwelt/'))
      .slice(0, 10),
  );

  console.log(JSON.stringify({
    url,
    title,
    challengeDetected,
    productLinks,
  }, null, 2));
} finally {
  await browser.close();
}
