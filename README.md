<div align="center">
  <img src="Assets/Sprites/Logo/SeedYourSpace.png" alt="Seed Your Space Logo" width="300"/>

# SYS - Seed Your Space 🌌

**A High-Fidelity, Deterministic Star System Builder for Unity URP**

[![Unity](https://img.shields.io/badge/Unity-6000.3.22f1-black?logo=unity)](#)
[![Render Pipeline](https://img.shields.io/badge/Pipeline-URP-blue)](#)
[![Web3](https://img.shields.io/badge/Network-Sepolia_ETH-purple)](#)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-lightgrey.svg)](LICENSE)

</div>

<br/>

> **🎓 Academic Note:** This repository contains the source code for a Bachelor's Degree thesis project in Computer Science (_Tesi di Laurea Triennale in Informatica_).

![SYS Showcase](Showcase/Draxis.png)

## 🔭 Overview

**SYS** is a procedural space diorama builder and astrophysics engine forged in Unity 6 using the Universal Render Pipeline (URP). It transmutes deterministic seeds into fully realized, visually breathtaking 3D star systems. The engine dynamically orchestrates central stars, planets, moons, and asteroid belts, driven by a rigorous framework of astrophysical rules and stochastic mathematics.

Designed to seamlessly blend high visual fidelity with blockchain technology, the architecture guarantees that a single master seed will consistently generate the exact same cosmic layout, ensuring absolute backward compatibility across different generation algorithm versions.

## ✨ Key Features

### 🌌 Procedural Astrophysics Engine

- **Deterministic Generation:** Relies on robust fundamental classes to derive consistent numerical seeds from alphanumeric strings.
- **Accurate Orbital Mechanics:** Calculates realistic orbital distances, eccentricities, and orbital inclinations based on astrophysical models.
- **Habitability & Atmospherics:** Determines planetary classes (Gas Giants, Ice Giants, Terrestrial), calculates surface temperatures via frost lines, and deduces atmospheric compositions.

### 🎨 High-Fidelity Rendering (URP)

- **Stellar Surfaces:** Procedural Voronoi granulation shaders bring churning stellar plasma to life.
- **Planetary & Lunar Topography:** Advanced procedural shaders dynamically generate consistent surface biomes based on underlying planetary data.
- **Atmospheric Auroras:** Worlds with specific magnetic and atmospheric conditions display dynamic polar auroras.
- **Cosmic Skybox & Nebulae:** Features a fully procedural skybox enriched with nebulae.
- **Procedural Meshes:** Custom Icosphere generators for celestial bodies optimize vertex distribution, entirely preventing polar distortion artifacts.
- **Ring Systems:** Procedural generation of intricate planetary and lunar rings.

### 🕹️ Interactive Simulation & View Modes

- **Cinematic Mode 🎥:** Hides the UI and engages a smooth, automated camera orbit around selected celestial bodies for a breathtaking showcase.
- **Orrery Mode 🧭:** Projects navigational orbital lines and rotational axes directly into the 3D space to visualize the system's mechanics.
- **Arcade Mode 🚀:** Populates the system with procedural spaceships actively shuttling between planets. _(Huge thanks to [Kenney](https://kenney.nl/) for the amazing Space Kit assets used here!)_
- **Temporal Control ⏳:** Granular time manipulation allows users to freeze the simulation entirely or accelerate cosmic time up to 100x.
- **Planetary Atlas & UI 🗺️:** Interacting with any celestial body reveals detailed astrophysical data and unfolds an interactive 2D atlas map, directly mirroring the 3D procedural mesh.
- **Retro Pixel Filter 👾:** A custom URP Scriptable Render Feature that applies a scalable pixelation effect (up to 10x intensity) for a nostalgic, low-res aesthetic.

### 🛠 Architecture & Tooling

- **Factory Pattern:** Clean, modular instantiation of `AstrophysicsRules` and generators to support multiple concurrent algorithm versions.
- **Dynamic Multipliers:** The `VisualDioramaBuilder` effortlessly translates astronomical scales into readable, aesthetically pleasing 3D environments with real-time scaling controls.

## 🔗 Web3 & Blockchain Integration

SYS is engineered to operate seamlessly with the Ethereum blockchain, currently deployed on the **Sepolia Testnet**. The architecture fiercely embraces the philosophy of "minimal on-chain data, maximal off-chain fidelity."

- **Minting & Chainlink VRF:** Users interact with the smart contract to mint a new star system NFT. Upon transaction, the contract queries **Chainlink VRF** for a verifiably random seed.
- **Ultra-Lightweight On-Chain Storage:** The resulting NFT is highly optimized. Its on-chain metadata stores only two critical pieces of information:
  1. The randomly generated `masterSeed` (string).
  2. The `algorithmVersion` (integer) active at the time of creation.
- **Deterministic Off-Chain Reconstruction:** When a player views or owns the NFT, the Unity client reads the seed and algorithm version. Because the SYS engine is strictly deterministic, it mathematically reconstructs the exact same planetary system, atmospheric compositions, and orbital layouts every single time—bypassing the need for centralized servers or bloated metadata files.

## 📦 Prerequisites & Dependencies

The project is built on **Unity 6000.3.22f1** and utilizes the following major packages:

- **Unity Core Packages:**
  - Universal Render Pipeline (URP) `17.3.0`
  - Visual Effect Graph `17.3.0`
  - Shader Graph `17.3.0`
  - Burst `1.8.30`
  - Mathematics `1.3.3`
  - Input System `1.20.0`
- **Third-Party & Web3 Packages:**
  - [Nethereum](https://nethereum.com/) `6.1.0` (for Web3/RPC smart contract communication)
  - Unified Blur `0.9.0` (by Luka Kldiashvili)

## 📄 License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**.
See the [LICENSE](LICENSE) file for more details.
