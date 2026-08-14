import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const viewports = [
  { name: 'mobile', width: 320, height: 720 },
  { name: 'tablet', width: 768, height: 900 },
  { name: 'desktop', width: 1440, height: 1000 },
] as const;

async function expectContained(page: Page) {
  const dimensions = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  expect(dimensions.scrollWidth).toBe(dimensions.clientWidth);
}

async function expectAccessible(page: Page) {
  const results = await new AxeBuilder({ page }).analyze();
  expect(results.violations).toEqual([]);
}

for (const viewport of viewports) {
  test(`${viewport.name} repository overview is contained and stable`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Repository overview' })).toBeVisible();
    await expectContained(page);
    await expectAccessible(page);
    await expect(page).toHaveScreenshot(`repository-overview-${viewport.name}.png`, { fullPage: true });
  });
}

test('dark repository analysis remains accessible and stable', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto('/');
  await page.getByLabel('Preview theme').selectOption('dark');
  await page.getByRole('button', { name: 'Explore repository' }).first().click();
  await page.getByRole('button', { name: /API endpoints/ }).click();
  await expect(page.getByRole('heading', { name: 'API endpoints' })).toBeVisible();
  await expectContained(page);
  await expectAccessible(page);
  await expect(page).toHaveScreenshot('repository-endpoints-dark-desktop.png', { fullPage: true });
});

test('mobile navigation exposes its state to assistive technology', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await page.goto('/');
  const menu = page.getByRole('button', { name: 'Open preview navigation' });
  await menu.click();
  await expect(page.getByRole('button', { name: 'Close preview navigation' })).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByRole('complementary')).toBeVisible();
  await expectAccessible(page);
});
