(async function () {
  const web = document.getElementById("web");
  const db = document.getElementById("db");

  async function check(path, element, success) {
    try {
      const response = await fetch(path, { cache: "no-store" });
      element.textContent = response.ok ? success : "Nicht bereit";
      element.className = response.ok ? "ok" : "error";
    } catch (_error) {
      element.textContent = "Nicht erreichbar";
      element.className = "error";
    }
  }

  await Promise.all([
    check("/health/live", web, "Online"),
    check("/health/ready", db, "Verbunden")
  ]);
})();
