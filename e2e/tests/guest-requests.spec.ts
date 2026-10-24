import { test, expect } from '@playwright/test';

// End-to-end: a front-desk agent logs a guest request and moves it through the workflow.
test('front desk can create a request and complete it', async ({ page }) => {
  const description = `E2E extra pillows ${Date.now()}`;

  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'GuestFlow' })).toBeVisible();

  // Availability tiles load from the API (served from Redis after the first call)
  await expect(page.getByText('Total rooms')).toBeVisible();

  // Create a request
  await page.getByLabel('Room number').fill('101');
  await page.getByLabel('Category').selectOption('Housekeeping');
  await page.getByLabel('Description').fill(description);
  await page.getByRole('button', { name: 'Create request' }).click();

  const row = page.getByTestId('request-row').filter({ hasText: description });
  await expect(row).toBeVisible();
  await expect(row.getByText('Open')).toBeVisible();

  // Open → In progress → Done
  await row.getByRole('button', { name: 'Mark in progress' }).click();
  await expect(row.getByText('In progress')).toBeVisible();
  await row.getByRole('button', { name: 'Mark done' }).click();
  await expect(row.getByText('Done')).toBeVisible();
});

test('unknown room shows a validation error', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Room number').fill('9999');
  await page.getByLabel('Description').fill('Should fail');
  await page.getByRole('button', { name: 'Create request' }).click();
  await expect(page.getByRole('alert')).toContainText('does not exist');
});
