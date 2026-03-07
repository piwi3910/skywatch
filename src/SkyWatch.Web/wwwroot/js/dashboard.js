(function () {
    "use strict";

    var STATUS_POLL_INTERVAL = 2000;
    var PREVIEW_REFRESH_INTERVAL = 5000;
    var statusTimer = null;
    var previewTimer = null;

    function initTabs() {
        var tabs = document.querySelectorAll(".tab");
        tabs.forEach(function (tab) {
            tab.addEventListener("click", function () {
                var target = tab.getAttribute("data-tab");

                tabs.forEach(function (t) {
                    t.classList.remove("active");
                });
                tab.classList.add("active");

                document.querySelectorAll(".tab-content").forEach(function (section) {
                    section.classList.remove("active");
                });

                var targetSection = document.getElementById(target);
                if (targetSection) {
                    targetSection.classList.add("active");
                }
            });
        });
    }

    function updateStatusBanner(data) {
        var banner = document.getElementById("status-banner");
        var text = document.getElementById("status-text");

        banner.classList.remove("safe", "unsafe", "warning");

        if (data.isSafe) {
            banner.classList.add("safe");
            text.textContent = "Safe - " + data.safetyState;
        } else if (data.safetyState === "Initializing" || data.safetyState === "Unknown") {
            banner.classList.add("warning");
            text.textContent = data.safetyState;
        } else {
            banner.classList.add("unsafe");
            text.textContent = "Unsafe - " + data.safetyState;
        }
    }

    function updateMetrics(data) {
        if (!data.captureStatus) {
            return;
        }

        var confidence = document.getElementById("metric-confidence");
        if (confidence) {
            confidence.textContent = (data.confidence * 100).toFixed(1) + "%";
        }
    }

    function updateSafetyDetails(data) {
        var state = document.getElementById("detail-state");
        var reasons = document.getElementById("detail-reasons");
        var explanation = document.getElementById("detail-explanation");
        var lastChange = document.getElementById("detail-last-change");

        if (state) {
            state.textContent = data.safetyState;
        }

        if (reasons) {
            reasons.textContent = data.reasons === "None" ? "No unsafe conditions" : data.reasons;
        }

        if (explanation) {
            explanation.textContent = data.explanation;
        }

        if (lastChange) {
            if (data.evaluatedAt) {
                var date = new Date(data.evaluatedAt);
                lastChange.textContent = date.toLocaleTimeString();
            } else {
                lastChange.textContent = "--";
            }
        }
    }

    function pollStatus() {
        fetch("/api/dashboard/status")
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Status request failed: " + response.status);
                }
                return response.json();
            })
            .then(function (data) {
                updateStatusBanner(data);
                updateMetrics(data);
                updateSafetyDetails(data);
            })
            .catch(function () {
                var banner = document.getElementById("status-banner");
                var text = document.getElementById("status-text");
                banner.classList.remove("safe", "unsafe", "warning");
                banner.classList.add("warning");
                text.textContent = "Connection lost";
            });
    }

    function refreshPreview() {
        var img = document.getElementById("preview-image");
        if (img) {
            img.src = "/api/preview/frame?t=" + Date.now();
        }
    }

    function start() {
        initTabs();
        pollStatus();
        refreshPreview();

        statusTimer = setInterval(pollStatus, STATUS_POLL_INTERVAL);
        previewTimer = setInterval(refreshPreview, PREVIEW_REFRESH_INTERVAL);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start);
    } else {
        start();
    }
})();
