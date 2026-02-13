# 0. Global Specs

- **Engine:** Unity
- **Units:** Metric only  
  - **1 Unity world unit = 1 meter**
  - Speed: **m/s** (UI may also show **km/h**)
  - Angles: degrees
- **Input:** Unity **New Input System**
- **Configurability:** Prefer ScriptableObjects for:
  - Car handling (per drivetrain, per axle, per tire)
  - Surface friction/effects
  - Road profile + generation params
  - Difficulty/scaling curves (threat speed, scoring multipliers, traffic density)
