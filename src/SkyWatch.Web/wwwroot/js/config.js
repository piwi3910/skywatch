(function () {
    "use strict";

    var currentProfile = null;

    var thresholdFields = [
        { key: "maxAverageBrightness", label: "Max Average Brightness", step: "1" },
        { key: "minStarCount", label: "Min Star Count", step: "1" },
        { key: "maxCloudinessScore", label: "Max Cloudiness Score", step: "0.01" },
        { key: "minEntropy", label: "Min Entropy", step: "0.1" },
        { key: "minEdgeDensity", label: "Min Edge Density", step: "0.001" },
        { key: "obstructionThreshold", label: "Obstruction Threshold", step: "0.01" },
        { key: "starDetectionThreshold", label: "Star Detection Threshold", step: "0.01" },
        { key: "confidenceThreshold", label: "Confidence Threshold", step: "0.01" }
    ];

    function loadProfile() {
        fetch("/api/config/profile")
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Failed to load profile: " + response.status);
                }
                return response.json();
            })
            .then(function (profile) {
                currentProfile = profile;
                renderThresholdEditor(profile.thresholds);
            })
            .catch(function (err) {
                showStatus("Failed to load profile: " + err.message, "error");
            });
    }

    function renderThresholdEditor(thresholds) {
        var editor = document.getElementById("threshold-editor");
        if (!editor) {
            return;
        }

        editor.innerHTML = "";

        thresholdFields.forEach(function (field) {
            var row = document.createElement("div");
            row.className = "threshold-row";

            var label = document.createElement("label");
            label.textContent = field.label;
            label.setAttribute("for", "threshold-" + field.key);

            var input = document.createElement("input");
            input.type = "number";
            input.id = "threshold-" + field.key;
            input.name = field.key;
            input.step = field.step;
            input.value = thresholds[field.key] !== undefined ? thresholds[field.key] : "";

            row.appendChild(label);
            row.appendChild(input);
            editor.appendChild(row);
        });
    }

    function gatherThresholds() {
        var thresholds = {};
        thresholdFields.forEach(function (field) {
            var input = document.getElementById("threshold-" + field.key);
            if (input) {
                thresholds[field.key] = parseFloat(input.value);
            }
        });
        return thresholds;
    }

    function saveConfig() {
        if (!currentProfile) {
            showStatus("No profile loaded", "error");
            return;
        }

        var updatedProfile = {
            name: currentProfile.name,
            description: currentProfile.description,
            capture: currentProfile.capture,
            thresholds: gatherThresholds(),
            timing: currentProfile.timing,
            masks: currentProfile.masks,
            manualOverride: currentProfile.manualOverride
        };

        fetch("/api/config/profile", {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(updatedProfile)
        })
            .then(function (response) {
                return response.json().then(function (data) {
                    return { ok: response.ok, data: data };
                });
            })
            .then(function (result) {
                if (result.ok) {
                    currentProfile = result.data;
                    showStatus("Configuration saved successfully", "success");
                } else {
                    var errors = result.data.errors || [];
                    showStatus("Validation errors: " + errors.join(", "), "error");
                }
            })
            .catch(function (err) {
                showStatus("Failed to save: " + err.message, "error");
            });
    }

    function loadProfileList() {
        fetch("/api/config/profiles")
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Failed to load profiles: " + response.status);
                }
                return response.json();
            })
            .then(function (profiles) {
                var select = document.getElementById("profile-select");
                if (!select) {
                    return;
                }

                select.innerHTML = "";
                profiles.forEach(function (name) {
                    var option = document.createElement("option");
                    option.value = name;
                    option.textContent = name;
                    select.appendChild(option);
                });
            })
            .catch(function (err) {
                showStatus("Failed to load profiles: " + err.message, "error");
            });
    }

    function activateProfile() {
        var select = document.getElementById("profile-select");
        if (!select || !select.value) {
            showStatus("No profile selected", "error");
            return;
        }

        var name = select.value;

        fetch("/api/config/profiles/" + encodeURIComponent(name) + "/activate", {
            method: "POST"
        })
            .then(function (response) {
                if (!response.ok) {
                    return response.json().then(function (data) {
                        throw new Error(data.error || "Activation failed");
                    });
                }
                return response.json();
            })
            .then(function (profile) {
                currentProfile = profile;
                renderThresholdEditor(profile.thresholds);
                showStatus("Profile '" + name + "' activated", "success");
            })
            .catch(function (err) {
                showStatus("Failed to activate profile: " + err.message, "error");
            });
    }

    function showStatus(message, type) {
        var status = document.getElementById("config-status");
        if (!status) {
            return;
        }

        status.textContent = message;
        status.className = type;

        if (type === "success") {
            setTimeout(function () {
                if (status.textContent === message) {
                    status.textContent = "";
                    status.className = "";
                }
            }, 3000);
        }
    }

    function init() {
        loadProfile();
        loadProfileList();

        var saveBtn = document.getElementById("save-config");
        if (saveBtn) {
            saveBtn.addEventListener("click", saveConfig);
        }

        var activateBtn = document.getElementById("activate-profile");
        if (activateBtn) {
            activateBtn.addEventListener("click", activateProfile);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
