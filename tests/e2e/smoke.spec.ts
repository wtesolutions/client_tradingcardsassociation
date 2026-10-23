import { test, expect } from "@playwright/test";

const hobby = "hobby-tyme-sports-cards-toys-mebane-nc";

test.describe("NTCA smoke (TESTING.md S1–S4, S7–S8)", () => {
  test("P01 home loads without a preview banner", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByTestId("header-join")).toHaveText(/Join the association/);
    await expect(page.getByTestId("home-search")).toBeVisible();
    await expect(page.locator("text=Preview mode — pages are live for QA")).toHaveCount(0);
  });

  test("P02 shops search UI is present", async ({ page }) => {
    await page.goto("/shops");
    await expect(page.getByTestId("shops-q")).toBeVisible();
    await expect(page.getByTestId("shops-search-submit")).toBeVisible();
    await expect(page.getByTestId("shops-result-count")).toBeVisible();
    await page.getByTestId("shops-q").fill("hobby");
    await page.getByTestId("shops-search-submit").click();
    await expect(page.getByTestId("shops-result-count")).toBeVisible();
  });

  test("P05 Hobby Tyme featured listing", async ({ page }) => {
    await page.goto(`/shops/nc/mebane/${hobby}`);
    await expect(page.getByTestId("shop-detail-name")).toContainText(/Hobby Tyme/i);
    await expect(page.getByTestId("shop-featured-ribbon")).toHaveText(/Featured/i);
    await expect(page.getByText("Sponsored")).toHaveCount(0);
  });

  test("P08 SkyBox manufacturer logo and description", async ({ page }) => {
    await page.goto("/manufacturers/skybox");
    await expect(page.getByTestId("mfr-logo")).toBeVisible();
    await expect(page.getByTestId("mfr-description")).toBeVisible();
  });

  test("P14 footer has no Staff or discovery links", async ({ page }) => {
    await page.goto("/");
    const footer = page.getByTestId("site-footer");
    await expect(footer).toBeVisible();
    await expect(footer.getByRole("link", { name: "Staff" })).toHaveCount(0);
    await expect(footer.locator('a[href="/robots.txt"]')).toHaveCount(0);
    await expect(footer.locator('a[href="/llms.txt"]')).toHaveCount(0);
    await expect(footer.locator('a[href="/sitemap.xml"]')).toHaveCount(0);
    await expect(footer.locator('a[href="/SADMIN"]')).toHaveCount(0);
  });

  test("P15 Preview sends noindex", async ({ request }) => {
    const res = await request.get("/");
    expect(res.ok()).toBeTruthy();
    const robots = res.headers()["x-robots-tag"] || "";
    expect(robots.toLowerCase()).toContain("noindex");
  });
});
