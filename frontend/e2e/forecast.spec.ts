import { expect, test } from '@playwright/test';

// Generate -> view a forecast: the named critical flow (ADR-006). Requires the API
// running with the Development demo seed reachable.
test('generate and view a forecast', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByTestId('status')).toHaveText('Ready', { timeout: 30_000 });

  await page.getByTestId('forecast-now').click();

  await expect(page.getByTestId('forecast-chart')).toBeVisible({ timeout: 60_000 });
});
