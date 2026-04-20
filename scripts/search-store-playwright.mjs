const args = process.argv.slice(2);

function parseArgs(argv) {
  const parsed = {
    url: '',
    host: '',
    limit: 40,
    matches: [],
  };

  for (let i = 0; i < argv.length; i += 1) {
    const key = argv[i];
    const value = argv[i + 1];

    if (!key?.startsWith('--')) {
      continue;
    }

    switch (key) {
      case '--url':
        parsed.url = value ?? '';
        i += 1;
        break;
      case '--host':
        parsed.host = value ?? '';
        i += 1;
        break;
      case '--limit':
        parsed.limit = Number.parseInt(value ?? '40', 10) || 40;
        i += 1;
        break;
      case '--match':
        parsed.matches.push(value ?? '');
        i += 1;
        break;
      default:
        break;
    }
  }

  return parsed;
}

const options = parseArgs(args);

if (!options.url || !options.host) {
  console.error('Missing required --url or --host argument.');
  process.exit(2);
}

const playwrightModulePath = process.env.PLAYWRIGHT_CORE_PATH ?? 'playwright';
const { chromium } = await import(playwrightModulePath);

const browser = await chromium.launch({ headless: true });

try {
  const page = await browser.newPage({
    viewport: { width: 1440, height: 900 },
    locale: 'de-AT',
    userAgent:
      'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36',
  });

  await page.goto(options.url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(6000);

  const title = await page.title();
  const content = await page.content();
  const challengeDetected =
    content.includes('Just a moment...') ||
    content.includes('Enable JavaScript and cookies to continue') ||
    content.includes('__cf_chl_opt');

  const normalizedHost = options.host.replace(/^www\./i, '').toLowerCase();

  const links = await page.$$eval(
    'a[href]',
    (anchors, { host, limit, matches }) => {
      const normalizedHostInner = host.replace(/^www\./i, '').toLowerCase();
      const preferred = [];
      const fallback = [];
      const seen = new Set();

      for (const anchor of anchors) {
        const href = anchor.getAttribute('href');
        if (!href) {
          continue;
        }

        try {
          const absolute = new URL(href, window.location.href);
          const absoluteHost = absolute.hostname.replace(/^www\./i, '').toLowerCase();
          if (!(absoluteHost === normalizedHostInner || absoluteHost.endsWith(`.${normalizedHostInner}`))) {
            continue;
          }

          const value = absolute.href;
          if (seen.has(value)) {
            continue;
          }

          seen.add(value);
          const lowerValue = value.toLowerCase();
          const isPreferred =
            Array.isArray(matches) &&
            matches.length > 0 &&
            matches.some((match) => lowerValue.includes(String(match).toLowerCase()));

          if (isPreferred) {
            preferred.push(value);
          } else {
            fallback.push(value);
          }

        } catch {
          // ignore invalid URLs from the page
        }
      }

      return [...preferred, ...fallback].slice(0, limit);
    },
    { host: normalizedHost, limit: options.limit, matches: options.matches },
  );

  const enrichedLinks = [...links];
  if (options.matches.length > 0 && enrichedLinks.length < options.limit) {
    const seen = new Set(enrichedLinks);
    const urlCandidates = content.match(/https?:\/\/[^"'\\\s<>]+|\/[^"'\\\s<>]+/g) ?? [];

    for (const candidate of urlCandidates) {
      const lowerCandidate = candidate.toLowerCase();
      if (!options.matches.some((match) => lowerCandidate.includes(String(match).toLowerCase()))) {
        continue;
      }

      try {
        const absolute = new URL(candidate, options.url);
        const absoluteHost = absolute.hostname.replace(/^www\./i, '').toLowerCase();
        if (!(absoluteHost === normalizedHost || absoluteHost.endsWith(`.${normalizedHost}`))) {
          continue;
        }

        const value = absolute.href;
        if (seen.has(value)) {
          continue;
        }

        seen.add(value);
        enrichedLinks.unshift(value);

        if (enrichedLinks.length >= options.limit) {
          break;
        }
      } catch {
        // ignore invalid URLs from raw HTML
      }
    }
  }

  console.log(JSON.stringify({
    url: options.url,
    title,
    challengeDetected,
    links: enrichedLinks.slice(0, options.limit),
  }, null, 2));
} finally {
  await browser.close();
}
