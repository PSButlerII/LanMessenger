//// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
//// for details on configuring this project to bundle and minify static web assets.

//// Write your JavaScript code.
////const connection = new signalR.HubConnectionBuilder()
////    .withUrl("/fileUploadHub")
////    .build();
////connection.start().catch(err => console.error(err.toString()));

////connection.on("fileUploaded", (f) => {
////    // display a link (see safe rendering note below)
////    appendFile(f);
////});


//const messagesEl = document.getElementById("messages");
//const senderEl = document.getElementById("sender");
//const textEl = document.getElementById("text");
//const sendBtn = document.getElementById("send");

//// Remember name
//senderEl.value = localStorage.getItem("lan_sender") || "";
//senderEl.addEventListener("change", () => localStorage.setItem("lan_sender", senderEl.value));

//function appendMessage(m) {
//    const div = document.createElement("div");
//    div.className = "mb-2";
//    div.innerHTML = `<span class="text-muted small">${m.timestamp}</span>
//                        <strong class="ms-2">${escapeHtml(m.sender)}:</strong>
//                        <span class="ms-1">${escapeHtml(m.text)}</span>`;
//    messagesEl.appendChild(div);
//    messagesEl.scrollTop = messagesEl.scrollHeight;
//}

//function escapeHtml(str) {
//    return (str ?? "")
//        .replaceAll("&", "&amp;")
//        .replaceAll("<", "&lt;")
//        .replaceAll(">", "&gt;")
//        .replaceAll('"', "&quot;")
//        .replaceAll("'", "&#039;");
//}

//const connection = new signalR.HubConnectionBuilder()
//    .withUrl("/chatHub")
//    .withAutomaticReconnect()
//    .build();

//connection.on("messageReceived", (m) => appendMessage(m));

//connection.on("fileUploaded", (f) => {
//    // display a link (see safe rendering note below)
//    appendFile(f);
//});

//function appendFile(f) {
//    const div = document.createElement("div");
//    div.className = "mb-2";

//    const ts = document.createElement("span");
//    ts.className = "text-muted small";
//    ts.textContent = f.timestamp;

//    const strong = document.createElement("strong");
//    strong.className = "ms-2";
//    strong.textContent = `${f.sender}:`;

//    const link = document.createElement("a");
//    link.className = "ms-2";
//    link.href = f.url;
//    link.target = "_blank";
//    link.rel = "noopener";
//    link.textContent = `📎 ${f.fileName}`;

//    const size = document.createElement("span");
//    size.className = "text-muted small ms-2";
//    size.textContent = `(${Math.round(f.size/1024)} KB)`;

//    div.appendChild(ts);
//    div.appendChild(strong);
//    div.appendChild(link);
//    div.appendChild(size);

//    messagesEl.appendChild(div);
//    messagesEl.scrollTop = messagesEl.scrollHeight;
//}


//async function sendMessage() {
//    const sender = (senderEl.value || "").trim() || "Unknown";
//    const text = (textEl.value || "").trim();
//    if (!text) return;

//    textEl.value = "";
//    await connection.invoke("SendMessage", sender, text);
//    textEl.focus();
//}

//sendBtn.addEventListener("click", sendMessage);
//textEl.addEventListener("keydown", (e) => {
//    if (e.key === "Enter") sendMessage();
//});

//connection.start().then(() => {
//    messagesEl.scrollTop = messagesEl.scrollHeight;
//    textEl.focus();
//});

//const fileEl = document.getElementById("file");
//const sendFileBtn = document.getElementById("sendFile");

//async function sendFile() {
//    const f = fileEl.files[0];
//    if (!f) return;

//    const sender = (senderEl.value || "").trim() || "Unknown";

//    const fd = new FormData();
//    fd.append("sender", sender);
//    fd.append("file", f);
//    fd.append("uploadKey", localStorage.getItem("uploadKey") || "");

//    const res = await fetch("/upload", { 
//        method: "POST",
//        body: fd,
//            headers: {
//            "X-Device-Id": getDeviceId(),
//            "X-Device-Key": getDeviceKey()
//        }
//    });
//    if (!res.ok) {
//    alert("Upload failed.");
//    return;
//    }

//    const info = await res.json();

//    // Show it locally right away
//    appendMessage({
//        timestamp: new Date().toLocaleTimeString(),
//        sender: info.sender,
//        text: `📎 <a href="${info.url}" target="_blank" rel="noopener">${info.fileName}</a> (${Math.round(info.size/1024)} KB)`
//    });

//    // Optional: broadcast using SignalR (next section)
//    fileEl.value = "";
//}

//    sendFileBtn.addEventListener("click", sendFile);

//function getDeviceId() {
//    let id = localStorage.getItem("deviceId");
//    if (!id) {
//        id = (navigator.userAgent.includes("Windows") ? "WIN-" : "DEV-") + crypto.randomUUID().slice(0, 8).toUpperCase();
//        localStorage.setItem("deviceId", id);
//    }
//    return id;
//}

//function getDeviceKey() {
//    return localStorage.getItem("deviceKey") || "";
//}

