# 6. Surfaces (Single Pipeline)

## 6.1 Data Encoding (Vertex Colors)
Road mesh vertex color channels encode surface mix:

- **R:** asphalt weight
- **G:** gravel/shoulder weight
- **B:** ice/snow weight
- **A:** wetness

Wheel raycast hit reads triangle vertex colors via **barycentric interpolation**.

## 6.2 Gameplay Use
- Surface mix feeds:
  - **Friction (μ)**
  - Tire audio + particles
  - Camera shake / haptics hooks
- Friction model:
  - Compute base μ from (asphalt/gravel/ice).
  - Apply wetness reduction.
  - Apply **combined friction ellipse**: braking + cornering share grip budget.
