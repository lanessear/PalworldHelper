const statusElement = document.querySelector("#status");

async function getJson(path) {
  const response = await fetch(path, { headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return response.json();
}

async function initialize() {
  try {
    const [health, about, stats] = await Promise.all([
      getJson("/api/health"),
      getJson("/api/about"),
      getJson("/api/stats")
    ]);

    statusElement.className = "status online";
    statusElement.innerHTML = "<span></span>Local service online";
    document.querySelector("#version").textContent = `${about.name} ${about.version}`;

    for (const key of ["species", "recipes", "collections", "pals"]) {
      document.querySelector(`#${key}`).textContent = stats[key].toLocaleString();
    }

    console.info("PalworldHelper health", health);
  } catch (error) {
    statusElement.className = "status offline";
    statusElement.innerHTML = "<span></span>Service unavailable";
    console.error(error);
  }
}

initialize();
