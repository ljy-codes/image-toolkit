const fs = require("node:fs");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { chromium } = require("playwright");

const htmlPath = path.resolve(
  process.argv[2] || "tmp/delivery-staging/产品介绍.html",
);
const outputDirectory = path.resolve(
  process.argv[3] || "tmp/html-validation",
);
const viewports = [
  { width: 390, height: 844 },
  { width: 768, height: 1024 },
  { width: 1024, height: 900 },
  { width: 1366, height: 900 },
  { width: 1920, height: 1080 },
  { width: 2560, height: 1080 },
];
const browserCandidates = [
  process.env.IMAGETOOLKIT_BROWSER_PATH,
  "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
  "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
  "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
].filter(Boolean);
const browserExecutable = browserCandidates.find((candidate) =>
  fs.existsSync(candidate),
);

if (!fs.existsSync(htmlPath)) {
  throw new Error(`找不到产品介绍 HTML：${htmlPath}`);
}

fs.mkdirSync(outputDirectory, { recursive: true });

(async () => {
  if (!browserExecutable) {
    throw new Error("未找到可用于无头验证的 Edge 或 Chrome。");
  }

  const browser = await chromium.launch({
    executablePath: browserExecutable,
    headless: true,
  });
  const results = [];

  try {
    for (const viewport of viewports) {
      const page = await browser.newPage({ viewport });
      await page.goto(pathToFileURL(htmlPath).href, {
        waitUntil: "networkidle",
      });
      await page.screenshot({
        path: path.join(
          outputDirectory,
          `产品介绍-${viewport.width}x${viewport.height}.png`,
        ),
        fullPage: true,
      });

      const metrics = await page.evaluate(() => {
        const main = document.querySelector("main");
        const hero = document.querySelector(".hero");
        const heading = document.querySelector("h1");
        const lead = document.querySelector(".lead");
        const rect = (element) => {
          const value = element.getBoundingClientRect();
          return {
            width: Math.round(value.width),
            height: Math.round(value.height),
            left: Math.round(value.left),
            right: Math.round(value.right),
          };
        };

        return {
          viewportWidth: window.innerWidth,
          documentWidth: document.documentElement.scrollWidth,
          bodyWidth: document.body.scrollWidth,
          main: rect(main),
          hero: rect(hero),
          heading: rect(heading),
          lead: rect(lead),
          headingWritingMode: getComputedStyle(heading).writingMode,
          leadWritingMode: getComputedStyle(lead).writingMode,
          headingText: heading.textContent.trim(),
        };
      });

      const minimumContentWidth = Math.min(320, viewport.width - 36);
      const errors = [];
      if (metrics.documentWidth > viewport.width + 1) {
        errors.push(
          `页面横向溢出 ${metrics.documentWidth - viewport.width}px`,
        );
      }
      if (metrics.hero.width < minimumContentWidth) {
        errors.push(`首屏宽度异常：${metrics.hero.width}px`);
      }
      if (metrics.lead.width < minimumContentWidth - 20) {
        errors.push(`介绍正文被压窄：${metrics.lead.width}px`);
      }
      if (metrics.heading.width < 90 || metrics.heading.height > 120) {
        errors.push(
          `标题疑似竖排或异常换行：${metrics.heading.width}x${metrics.heading.height}`,
        );
      }
      if (
        metrics.headingWritingMode !== "horizontal-tb" ||
        metrics.leadWritingMode !== "horizontal-tb"
      ) {
        errors.push("检测到非预期纵向书写模式");
      }

      results.push({
        viewport,
        metrics,
        passed: errors.length === 0,
        errors,
      });
      await page.close();
    }
  } finally {
    await browser.close();
  }

  const reportPath = path.join(outputDirectory, "report.json");
  fs.writeFileSync(reportPath, JSON.stringify(results, null, 2), "utf8");
  for (const result of results) {
    const state = result.passed ? "PASS" : "FAIL";
    console.log(
      `${state} ${result.viewport.width}x${result.viewport.height} ` +
        `document=${result.metrics.documentWidth}px ` +
        `hero=${result.metrics.hero.width}px ` +
        `lead=${result.metrics.lead.width}px`,
    );
    for (const error of result.errors) {
      console.log(`  ${error}`);
    }
  }

  if (results.some((result) => !result.passed)) {
    process.exitCode = 1;
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
