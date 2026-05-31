# Spiral Analytics Assistant: Immersive AR Data Discovery System

"Spiral Analytics Assistant" is an end-to-end Immersive Analytics platform that bridges the gap between raw data complexity and intuitive human-computer interaction. By leveraging an autonomous AI decision-maker (Gemini 2.5 Pro), the system automates data preprocessing, correlation analysis, and chart selection, delivering interactive 3D visualizations directly into the user's spatial environment via Augmented Reality (AR).

---

## 🚀 Key Features
* **Autonomous AI Analysis:** Automatically identifies data schemas, detects multivariate correlations, and suggests optimal 3D visualization types.
* **Immersive Analytics:** Visualizes data in 3D space using Voxel Density, Multi-Line Charts, and Bubble Plots, breaking the 2D screen limitation.
* **Dynamic PCP Exploration:** Overcomes the "spaghetti effect" in high-dimensional data using interactive, draggable virtual axes and real-time categorical color scaling.
* **Cross-Domain Compatibility:** Domain-agnostic architecture validated across 8+ different datasets (Logistics, Finance, Healthcare, HR, etc.).
* **Real-time Interaction:** Features Raycasting-based tooltip inspection and 60 FPS spatial manipulation on mobile AR devices.

---

## 🏗️ System Architecture
The system is built on a modular client-server architecture:
* **Server Layer (Python/FastAPI):** Orchestrates data analysis via `analyzer.py`, rendering via `chart_generator.py`, and API traffic via `main.py`.
* **Client Layer (Unity/C#):** Provides the AR interface, spatial axis management via `ARAxisController.cs`, and the Dynamic PCP orchestration system.

---

## 🛠️ Installation & Setup

### 1. Server Setup (Python/FastAPI)
Ensure you have Python 3.10+ installed.

```bash
# Clone the repository
git clone [https://github.com/YOUR_USERNAME/Spiral-Analytics-Assistant.git](https://github.com/YOUR_USERNAME/Spiral-Analytics-Assistant.git)
cd Server

# Install dependencies
pip install -r requirements.txt
```
## 🛠️ Client Setup (Unity/C#)
1. Open the `/Client` folder in **Unity Hub** (Recommended version: 2022.3 LTS or later).
2. Ensure the **AR Foundation** and **XR Interaction Toolkit** packages are installed in your project.
3. Configure the `apiUrl` variable within `NetworkManager.cs` to match your local server's IP address (e.g., `http://192.168.1.X:8000/api/v1/upload`).
4. Set your build target to your preferred mobile platform (**Android** or **iOS**) and build the application.

---

## 🎥 Demonstration
Watch the full case study showcase, where we test the system across 7 diverse datasets. 

[📺 Watch the Full Case Study Showcase on YouTube](https://youtu.be/trgpN7NwnNA?si=tiZISs6uods56LwU)

**Video Timeline:**
- **00:00** - Introduction & System Overview
- **00:04** - Drone Logistics Network (Synthetic Data)
- **00:32** - FAANG Finance (Time-series Analysis)
- **01:18** - Lifestyle & Health Risk Prediction
- **02:36** - McDonald's Menu 
- **03:25** - Pokemon Dataset
- **04:21** - Titanic Survival 
- **05:31** - IBM HR Analytics 
---

## 📂 Project Structure
* `/Server`: FastAPI backend scripts and Gemini AI integration.
* `/Client`: Unity project files, including `DynamicARManager.cs`, `ARAxisController.cs`, and AR interaction logic.
* `/data`: Sample datasets used for case studies (CSV/XLSX).

---

## 📖 Case Studies
We validated the system using 8 diverse datasets. Detailed findings are documented in the **Results & Discussion** section of the project report, covering:

* **Autonomous Drone Logistics:** 3D spatial relationship analysis of flight telemetry.
* **FAANG Finance:** Time-series financial trend visualization.
* **IBM HR Analytics:** High-dimensional employee attrition analysis using Dynamic PCP.
* *...and more.*

---



