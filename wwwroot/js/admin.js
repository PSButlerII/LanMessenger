
    (() => {
        const adminKeyInput = document.getElementById("adminKey");
        const statusEl = document.getElementById("status");
        const deviceList = document.getElementById("deviceList");

        const deviceIdEl = document.getElementById("deviceId");
        const deviceKeyEl = document.getElementById("deviceKey");

        const saveKeyBtn = document.getElementById("saveKeyBtn");
        const clearKeyBtn = document.getElementById("clearKeyBtn");
        const testBtn = document.getElementById("testBtn");

        const genKeyBtn = document.getElementById("genKeyBtn");
        const addBtn = document.getElementById("addBtn");
        const refreshBtn = document.getElementById("refreshBtn");

        const clientSnippetEl = document.getElementById("clientSnippet");
        const buildSnippetBtn = document.getElementById("buildSnippetBtn");
        const copySnippetBtn = document.getElementById("copySnippetBtn");

        const KEY_NAME = "LanMessenger.AdminKey";

        function setStatus(text, ok = null) {
            statusEl.textContent = text;
            statusEl.classList.remove("ok", "bad");
            if (ok === true) statusEl.classList.add("ok");
            if (ok === false) statusEl.classList.add("bad");
        }

        function loadAdminKey() {
            const k = sessionStorage.getItem(KEY_NAME) || "";
            adminKeyInput.value = k;
            return k;
        }

        function saveAdminKey() {
                const k = adminKeyInput.value.trim();
            sessionStorage.setItem(KEY_NAME, k);
            return k;
         }

        function clearAdminKey() {
            sessionStorage.removeItem(KEY_NAME);
            adminKeyInput.value = "";
        }

        function generateKey() {
            // 32 bytes => base64 ~44 chars
            const bytes = new Uint8Array(32);
            crypto.getRandomValues(bytes);
            // base64
            let bin = "";
                bytes.forEach(b => bin += String.fromCharCode(b));
            return btoa(bin);
        }

        async function apiGetDevices(adminKey) {
                const res = await fetch("/admin/devices", {
            method: "GET",
            headers: {"X-Admin-Key": adminKey }
                });
            if (!res.ok) throw new Error(`GET /admin/devices failed (${res.status})`);
            return await res.json();
        }

        async function apiPostDevice(adminKey, payload) {
                const res = await fetch("/admin/devices", {
            method: "POST",
            headers: {
                "X-Admin-Key": adminKey,
            "Content-Type": "application/json"
                        },
            body: JSON.stringify(payload)
                    });
            const text = await res.text();
            if (!res.ok) throw new Error(`POST /admin/devices failed (${res.status}): ${text}`);
            buildClientSnippet();
            return text ? JSON.parse(text) : { };
        }

        function renderDevices(devices) {
            deviceList.innerHTML = "";
            if (!devices || devices.length === 0) {
                const li = document.createElement("li");
                li.textContent = "(none)";
                deviceList.appendChild(li);
                return;
            }

            devices.forEach(id => {
                            const li = document.createElement("li");
                li.style.marginBottom = ".35rem";

                const row = document.createElement("div");
                row.className = "inline";
                row.style.justifyContent = "space-between";

                const left = document.createElement("div");
                left.textContent = id;

                const revokeBtn = document.createElement("button");
                revokeBtn.className = "danger tight";
                revokeBtn.textContent = "Revoke";
                revokeBtn.addEventListener("click", async () => {
                    const adminKey = sessionStorage.getItem(KEY_NAME) || "";
                    if (!adminKey) return setStatus("Missing Admin Key.", false);

                    if (!confirm(`Revoke device "${id}"?`)) return;

                    try {
                        await apiPostDevice(adminKey, { action: "revoke", deviceId: id });
                        setStatus(`Revoked: ${id}`, true);
                        await refresh();
                    } catch (e) {
                        setStatus(e.message, false);
                    }
                });

                row.appendChild(left);
                row.appendChild(revokeBtn);
                li.appendChild(row);
                deviceList.appendChild(li);
            });
        }

        async function refresh() {
                const adminKey = sessionStorage.getItem(KEY_NAME) || "";
            if (!adminKey) {
                renderDevices([]);
                setStatus("Status: missing Admin Key", false);
                return;
            }

        try {
            setStatus("Loading devices...", null);
            const data = await apiGetDevices(adminKey);
            renderDevices(data.devices || []);
            setStatus("Connected. Devices loaded.", true);
                } catch (e) {
            renderDevices([]);
            setStatus(e.message, false);
                }
        }

            // Wire up buttons
            saveKeyBtn.addEventListener("click", async () => {
            saveAdminKey();
        await refresh();
            });

            clearKeyBtn.addEventListener("click", () => {
            clearAdminKey();
        renderDevices([]);
        setStatus("Status: not connected", null);
            });

        testBtn.addEventListener("click", refresh);
        refreshBtn.addEventListener("click", refresh);

            genKeyBtn.addEventListener("click", () => {
            deviceKeyEl.value = generateKey();
            });

            addBtn.addEventListener("click", async () => {
                const adminKey = sessionStorage.getItem(KEY_NAME) || "";
        if (!adminKey) return setStatus("Missing Admin Key.", false);

        const deviceId = deviceIdEl.value.trim();
        const deviceKey = deviceKeyEl.value.trim();

        if (!deviceId || !deviceKey) {
            setStatus("Device ID and Device Key are required.", false);
        return;
                }

        try {
            await apiPostDevice(adminKey, { action: "add", deviceId, deviceKey });
        setStatus(`Added/updated device: ${deviceId}`, true);
        deviceKeyEl.value = "";
        await refresh();
                } catch (e) {
            setStatus(e.message, false);
                }

            });

        function buildClientSnippet() {
                const host = location.origin; // ensures correct host/port
        const deviceId = deviceIdEl.value.trim();
        const deviceKey = deviceKeyEl.value.trim();

        if (!deviceId || !deviceKey) {
            setStatus("Device ID and Device Key are required to build a snippet.", false);
        return;
                }

        const snippet =
        `# LanMessenger client test (run on client machine)
        $deviceId = "${deviceId}"
        $deviceKey = "${deviceKey}"

        "hello from $env:COMPUTERNAME" | Out-File .\\lan_test.txt -Encoding utf8

        curl "${host}/upload" -Method Post -Form @@{
            sender = "$env:COMPUTERNAME"
                      file   = Get-Item .\\lan_test.txt
                    } -Headers @@{
            "X-Device-Id" = $deviceId
                      "X-Device-Key" = $deviceKey
                    } -UseBasicParsing -Verbose
        `;

        clientSnippetEl.value = snippet;
        setStatus("Client snippet built.", true);
            }
        buildSnippetBtn.addEventListener("click", buildClientSnippet);

            copySnippetBtn.addEventListener("click", async () => {
              try {
            await navigator.clipboard.writeText(clientSnippetEl.value || "");
        setStatus("Copied snippet to clipboard.", true);
              } catch {
            setStatus("Clipboard copy failed (browser permissions). Select + copy manually.", false);
              }
            });


        // Init
        loadAdminKey();
        refresh();
    })();
