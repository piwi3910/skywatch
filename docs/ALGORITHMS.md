# SkyWatch Algorithms

This document describes the image analysis and safety decision algorithms as implemented in the codebase. All vision analyzers operate on grayscale images after preprocessing (resize, mask application).

## Pipeline Overview

The `FrameAnalyzerPipeline` orchestrates analysis in this order:

1. Preprocess frame (convert to grayscale, apply masks)
2. Compute brightness
3. Detect stars
4. Compute entropy
5. Compute edge density
6. Compute cloudiness (uses star count)
7. Detect obstruction (uses entropy, edge density, star count)
8. Compute overall confidence score

All results are bundled into a `SkyMetrics` object and passed to the safety evaluator.

Source: `src/SkyWatch.Vision/FrameAnalyzerPipeline.cs`

## 1. Brightness Analysis

**Class:** `BrightnessAnalyzer` (`src/SkyWatch.Vision/Analyzers/BrightnessAnalyzer.cs`)

**Method:** Computes the mean pixel value of the grayscale image using `Cv2.Mean()`.

**Output:** A value from 0.0 (completely dark) to 255.0 (completely white).

**Interpretation:** Low values indicate a dark sky (typical for nighttime imaging). High values indicate daylight, moon glow, light pollution, or an overexposed frame. The threshold `MaxAverageBrightness` (default 200.0) marks the upper limit; values above it trigger an unsafe condition.

## 2. Star Detection

**Class:** `StarDetector` (`src/SkyWatch.Vision/Analyzers/StarDetector.cs`)

**Method:** Template matching with a synthetic Gaussian star profile, followed by deduplication.

**Parameters:**
- Template size: 15x15 pixels
- Star radius: 3 pixels
- Gaussian sigma: 1.5
- Deduplication distance: 10 pixels

**Algorithm:**
1. Create a synthetic star template: a 15x15 black image with a filled white circle (radius 3) at the center, blurred with a 5x5 Gaussian kernel (sigma 1.5).
2. Run `Cv2.MatchTemplate` with `CCoeffNormed` (normalized cross-correlation).
3. Threshold the correlation result at the configurable `StarDetectionThreshold` (default 0.6).
4. Convert to a binary mask and collect all above-threshold pixel locations.
5. Deduplicate: iterate through candidate points, keeping only those that are at least 10 pixels (Euclidean distance) from all previously kept points.
6. Return the count of kept points.

**Output:** Integer star count. The threshold `MinStarCount` (default 10) is the minimum for safe conditions.

## 3. Cloudiness Analysis

**Class:** `CloudinessAnalyzer` (`src/SkyWatch.Vision/Analyzers/CloudinessAnalyzer.cs`)

**Method:** Weighted combination of star visibility ratio and contrast analysis.

**Weights:**
- Star ratio contribution: 0.7 (70%)
- Contrast contribution: 0.3 (30%)

**Algorithm:**
1. Compute star ratio: `1.0 - (starCount / baselineStarCount)`, clamped to [0, 1]. The baseline is `MinStarCount` from thresholds (minimum 10). Fewer stars relative to the baseline means higher cloudiness.
2. Compute contrast factor: calculate the standard deviation of the grayscale image using `Cv2.MeanStdDev`. Normalize by dividing by 80.0 (maximum expected standard deviation). The contrast factor is `1.0 - normalized_stddev`, clamped to [0, 1]. A uniform (low contrast) image suggests cloud cover.
3. Final score: `0.7 * starRatio + 0.3 * contrastFactor`, clamped to [0, 1].

**Output:** A score from 0.0 (clear sky) to 1.0 (fully overcast). The threshold `MaxCloudinessScore` (default 0.7) marks the unsafe boundary.

## 4. Entropy Calculation

**Class:** `EntropyCalculator` (`src/SkyWatch.Vision/Analyzers/EntropyCalculator.cs`)

**Method:** Shannon entropy of the grayscale intensity histogram.

**Algorithm:**
1. Compute a 256-bin histogram of the grayscale image using `Cv2.CalcHist`.
2. For each bin with a nonzero count, compute the probability `p = count / totalPixels`.
3. Accumulate: `entropy -= p * log2(p)`.

**Output:** A value from 0.0 to 8.0 (theoretical maximum for 8-bit images).

**Interpretation:**
- Low entropy (near 0): the image is nearly uniform (e.g., lens cap on, completely overcast, solid color).
- High entropy (5--7): the image has varied detail (stars, clouds, terrain).
- The threshold `MinEntropy` (default 2.0) flags images below it as potentially obstructed or featureless.

## 5. Edge Density Analysis

**Class:** `EdgeDensityAnalyzer` (`src/SkyWatch.Vision/Analyzers/EdgeDensityAnalyzer.cs`)

**Method:** Canny edge detection followed by density calculation.

**Algorithm:**
1. Apply Gaussian blur with a 3x3 kernel (sigma auto) to reduce noise.
2. Run Canny edge detection with thresholds 50 (low) and 200 (high).
3. Count nonzero pixels in the edge image.
4. Divide by total pixels: `density = nonzeroCount / (rows * cols)`.

**Output:** A value from 0.0 (no edges) to 1.0 (all edges). Typical sky images produce values in the 0.01--0.10 range. The threshold `MinEdgeDensity` (default 0.005) flags featureless images.

## 6. Obstruction Detection

**Class:** `ObstructionDetector` (`src/SkyWatch.Vision/Analyzers/ObstructionDetector.cs`)

**Method:** Combines multiple signals into a composite obstruction score.

**Scoring (additive, clamped to [0, 1]):**
- Entropy below 2.0: +0.4
- Edge density below 0.005: +0.3
- Star count equal to 0: +0.2
- Quadrant uniformity anomaly: +0.1

**Quadrant Uniformity Analysis:**
1. Split the image into four quadrants (top-left, top-right, bottom-left, bottom-right).
2. Compute the mean and standard deviation of each quadrant.
3. Compute the overall mean of the four quadrant means.
4. If any quadrant has a standard deviation below 5.0 AND its mean differs from the overall mean by more than 30.0, this indicates a partial obstruction (e.g., a dark region covering part of the lens). Score +0.1.

**Output:** A score from 0.0 (no obstruction) to 1.0 (fully obstructed). The threshold `ObstructionThreshold` (default 0.8) determines the unsafe boundary.

**Typical scenarios:**
- Lens cap on: low entropy + low edges + zero stars = 0.9+
- Condensation: some entropy but low edges and few/no stars = 0.5--0.7
- Clear sky: high entropy, edges present, stars detected = near 0.0

## 7. Confidence Score

**Class:** `FrameAnalyzerPipeline.ComputeConfidence` (private method)

**Method:** Average of six per-metric confidence values, each measuring how far the metric is from its unsafe threshold.

**Per-metric confidence:**
- Brightness: `1.0 - (brightness / MaxAverageBrightness)`
- Stars: `starCount / MinStarCount`
- Cloudiness: `1.0 - (cloudiness / MaxCloudinessScore)`
- Entropy: `entropy / (MinEntropy * 2.0)`
- Edge density: `edgeDensity / (MinEdgeDensity * 2.0)`
- Obstruction: `1.0 - (obstruction / ObstructionThreshold)`

Each is clamped to [0, 1]. The final confidence is the arithmetic mean of all six, clamped to [0, 1].

## 8. Safety Decision Engine

**Class:** `SafetyEvaluatorEngine` (`src/SkyWatch.Safety/SafetyEvaluatorEngine.cs`)

The engine runs a deterministic rule-based evaluation pipeline:

### Evaluation Order

1. **Manual override check:** If `ManualOverride` is `ForceSafe` or `ForceUnsafe`, return immediately with confidence 1.0.
2. **Analysis failure check:** If the frame analysis failed, return Unsafe with reason `AnalysisFailed`.
3. **Rule evaluation:** Run all six safety rules against the current metrics. Each rule compares one metric against its threshold and returns an `UnsafeReason` flag (or `None`). Rules are combined with bitwise OR (multiple reasons can fire simultaneously).
4. **Proposed state:** If any reasons are set, propose Unsafe; otherwise propose Safe.
5. **Warmup check:** If within `WarmupSeconds` of startup, return Initializing (not safe).
6. **Hysteresis:** Apply debounce timing to the proposed state transition.

### Safety Rules

| Rule | Metric | Condition for Unsafe |
|------|--------|---------------------|
| `BrightnessRule` | `AverageBrightness` | `> MaxAverageBrightness` |
| `StarCountRule` | `StarCount` | `< MinStarCount` |
| `CloudinessRule` | `CloudinessScore` | `> MaxCloudinessScore` |
| `ObstructionRule` | `ObstructionScore` | `> ObstructionThreshold` |
| `EntropyRule` | `Entropy` | `< MinEntropy` |
| `EdgeDensityRule` | `EdgeDensity` | `< MinEdgeDensity` |

Source: `src/SkyWatch.Safety/Rules/`

## 9. Hysteresis

**Class:** `HysteresisTracker` (`src/SkyWatch.Safety/HysteresisTracker.cs`)

Prevents oscillation near threshold boundaries by requiring proposed state changes to persist for a debounce period before the actual transition occurs.

### Transition Rules

**Safe to Unsafe:** The proposed Unsafe state must persist for `UnsafeDebounceSeconds` (default 10s) before the system transitions. This provides quick protection when conditions deteriorate.

**Unsafe to Safe:** Two conditions must be met:
1. The system must have been Unsafe for at least `MinimumUnsafeHoldSeconds` (default 60s) since the last transition.
2. The proposed Safe state must persist for `SafeRecoveryDebounceSeconds` (default 300s / 5 minutes) before the system transitions. This ensures conditions are genuinely stable before resuming operations.

**Initializing to Safe/Unsafe:** After the warmup period, transitions use the `UnsafeDebounceSeconds` debounce.

**Tracking mechanism:** The tracker records the proposed state and the time it was first proposed. If the proposed state changes before the debounce period elapses, the timer resets. If the proposed state matches the current state, the tracker resets (no transition needed).
