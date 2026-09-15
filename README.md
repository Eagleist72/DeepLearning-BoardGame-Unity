# Island Collapse (Zen Strategy)

*A Zen-inspired, turn-based 3D tactical grid puzzle game powered by Unity Sentis Neural Networks.*

![Unity](https://img.shields.io/badge/Unity-2023%2B-blue?logo=unity) ![URP](https://img.shields.io/badge/Render%20Pipeline-URP-lightgray) ![Sentis](https://img.shields.io/badge/AI-Unity%20Sentis-orange) ![DOTween](https://img.shields.io/badge/Animation-DOTween-yellow) ![C#](https://img.shields.io/badge/Language-C%23-239120) ![Performance](https://img.shields.io/badge/Performance-60%20FPS%20Mobile-brightgreen)

## 🎮 Gameplay Showcase

![Gameplay Showcase](docs/gameplay_demo.gif)
*(Note: Placeholder for Gameplay Demo Video / Animated GIF)*

### Core Rules
1. **Hop** to an adjacent tile.
2. **Crumble:** The tile you just left crumbles into the abyss.
3. **Trap** your opponent to claim the island!

## ✨ Key Features

- **Game Modes:** Play Solo vs Neural AI (Easy, Medium, Hard tiers) or enjoy Local Pass & Play (2P) with a friend.
- **Aesthetic UI/UX:** Soothing pastel Zen color palette, responsive 9-slice vector assets (PastelUI), and custom Pixeloid typography for a polished look.
- **Dynamic Game Feel (Juice):** Features organic, humanized AI thinking delays, tactile haptics, pooled spatial SFX, and procedural DOTween camera and tile animations for an immersive experience.

## 🏗️ Technical Architecture & Engineering Highlights

- **On-Device Neural AI (Unity Sentis):**
  - **Architecture:** Multi-Layer Perceptron Regressor (`MLPRegressor` via `scikit-learn`).
  - **Inference Pipeline:** Trained model exported directly to `.onnx` and executed on-device using `Unity Sentis` / `Unity Inference Engine`.
  - **Input Representation:** Encodes current normalized board tile occupancy, relative positions, and adjacent degrees of freedom into a flat feature vector.
  - **Decision & Difficulty Calibration:** Output predictions drive move selection with tiered logic:
    - **Easy:** Probabilistic error injection with fallback neighbor hops.
    - **Medium:** Temperature-scaled sampling over valid tile probabilities.
    - **Hard:** Strict Argmax/Greedy selection of the model's highest-confidence output.
- **Zero-Allocation SFX & Object Pooling:** Memory-safe audio management with pitch randomization and zero Garbage Collection (GC) spikes during intense tile destruction loops.
- **Mobile Performance (60 FPS Locked):** Highly optimized URP pipeline including tight shadow cascades, 4x MSAA, disabled HDR, and zero-latency touch raycasting with UI bleed prevention.

## 🧰 Tech Stack & Dependencies

| Category | Technology / Package |
| :--- | :--- |
| **Engine** | Unity 2023+ |
| **Rendering** | Universal Render Pipeline (URP) |
| **AI / Machine Learning** | Unity Sentis / Inference Engine |
| **Animation** | DOTween (Demigiant) |
| **UI Text** | TextMeshPro |
| **Input** | New Input System |

## 🚀 Controls & Getting Started

### Prerequisites
- Unity 2023.x or higher with Android/iOS build support.

### Installation
1. **Clone the repository:**
   ```bash
   git clone https://github.com/Eagleist72/DeepLearning-BoardGame-Unity.git
   ```
2. **Open the project:**
   Launch Unity Hub, click `Open`, and select the cloned `DeepLearning-BoardGame-Unity` directory.
3. **Build for Mobile:**
   - Go to `File > Build Settings`.
   - Select either `Android` or `iOS` and click `Switch Platform`.
   - Click `Build And Run` with your device connected.

### Controls
- **Touch / Click:** Select an adjacent valid tile to move.
- **UI Interaction:** Tap UI elements for game modes and settings.
