# DS4MTHACK – Advanced DualShock 4 Virtual Input System

**DS4MTHACK** is a high-performance virtual controller input system designed to emulate a DualShock 4 controller with advanced mouse-to-analog translation. Built with flexibility and precision in mind, the project allows developers, enthusiasts, and input engineers to simulate high-fidelity analog stick behavior using raw mouse input — ideal for research, prototyping, or accessibility-related automation.

---

## 🎯 Key Features

- 🎮 **DualShock 4 Emulation**  
  Virtual controller output via [ViGEmBus](https://github.com/ViGEm/ViGEmBus), enabling full compatibility with any game or application that supports DS4 input.

- 🖱️ **Mouse-to-Right Analog Stick Mapping**  
  - Configurable **acceleration curve** (exponential)
  - Adjustable **sensitivity** for X and Y axes independently
  - **Deadzone control** for fine-tuned movement
  - **Smoothing engine** with moving average or exponential decay
  - Cursor reset based on **screen center delta**, enabling compatibility with games that lock or hide the mouse

- 🧰 **Macro Engine**
  - Visual macro editor with step-by-step input logic
  - Assign macros to any DS4 button
  - Loopable execution with configurable hold and wait times
  - Support for multiple macro profiles (sets)

- 🔐 **Input & Process Masking**
  - Custom system title and process disguise
  - Hooked key and mouse inputs to prevent conflict or leakage
  - Optional "stealth mode" for reduced detection surface

- 🛡️ **Process Watchdog (Optional)**
  - Monitors for specific game executables
  - Automatically terminates or prompts shutdown if a target process is found (user configurable)

---

## 📦 Requirements

- Windows 10/11 x64
- [.NET Framework 4.8+](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
- [ViGEmBus Driver](https://github.com/ViGEm/ViGEmBus) (must be installed)
- `wininput_helper64.dll` (included separately for system integration)

---

## 🛠️ How It Works

1. Mouse movement is captured relative to the screen center (`delta`).
2. Delta is processed through:
   - A smoothing filter (average or exponential)
   - A custom acceleration curve
   - Deadzone logic
3. Final values are mapped to the 0–255 analog range and sent to a virtual DS4 controller.
4. Optionally, macros and button remapping are layered on top, with full UI control.

---

## 🧪 Use Cases

- Input testing and automation
- Accessibility solutions
- Input research & latency measurement
- Prototyping advanced analog behavior
- Gamepad behavior emulation for custom devices

---


## 🧷 Disclaimer

This project is intended for educational and development purposes only.  
It is **not** intended for use in online games or any scenario that violates terms of service or fair play policies.

> Use responsibly. The authors assume **no liability** for misuse.

---

## 📫 Contribution & Support

Contributions are welcome via pull requests or issue submissions.  
For discussions, feature suggestions, or improvements, feel free to open a thread.

---

**DS4MTHACK** — Precision analog input, virtualized.

---
