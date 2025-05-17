using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualBasic.Devices;
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;

namespace DS4MTHACK
{
    // Movido para fora da classe MainForm para ser acessível
    public enum DS4ButtonType
    {
        // Standard buttons
        Cross,
        Circle,
        Square,
        Triangle,
        ShoulderLeft,
        ShoulderRight,
        TriggerLeft,
        TriggerRight,
        ThumbLeft,
        ThumbRight,
        Share,
        Options,

        // D-Pad directions
        DPadUp,
        DPadRight,
        DPadDown,
        DPadLeft,

        // Special buttons
        PS,
        Touchpad
    }

    // Classe para ofuscação de strings
    public static class StringObfuscator
    {
        private static readonly byte[] Key = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };
        private static readonly byte[] IV = new byte[] { 0xFE, 0xDC, 0xBA, 0x98, 0x76, 0x54, 0x32, 0x10 };

        public static string Obfuscate(string input)
        {
            try
            {
                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(Key, IV), CryptoStreamMode.Write))
                        {
                            cs.Write(inputBytes, 0, inputBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                return input; // Fallback em caso de erro
            }
        }

        public static string Deobfuscate(string input)
        {
            try
            {
                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
                {
                    byte[] inputBytes = Convert.FromBase64String(input);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(Key, IV), CryptoStreamMode.Write))
                        {
                            cs.Write(inputBytes, 0, inputBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch
            {
                return input; // Fallback em caso de erro
            }
        }

        // Strings ofuscadas pré-definidas
        private static readonly Dictionary<string, string> ObfuscatedStrings = new Dictionary<string, string>
        {
            { "DS4", "RXpUbHZnPT0=" },
            { "ViGEm", "UmtGRFJrVT0=" },
            { "Warzone", "VW1GdGVHOXVaUT09" },
            { "Controller", "UTI5dWRISnZiR3hsY2c9PQ==" },
            { "Hack", "U0dGamF3PT0=" }
        };

        public static string GetString(string key)
        {
            if (ObfuscatedStrings.ContainsKey(key))
                return Deobfuscate(ObfuscatedStrings[key]);
            return key;
        }
    }

    // Classe para monitorar processos
    public class ProcessWatchdog
    {
        private System.Windows.Forms.Timer watchdogTimer;
        private readonly string[] targetProcesses = { "cod.exe", "ModernWarfare.exe", "BlackOpsColdWar.exe", "Warzone.exe" };
        private readonly Action onDetected;

        public ProcessWatchdog(Action onDetected)
        {
            this.onDetected = onDetected;
            watchdogTimer = new System.Windows.Forms.Timer();
            watchdogTimer.Interval = 5000; // Verificar a cada 5 segundos
            watchdogTimer.Tick += WatchdogTimer_Tick;
        }

        private void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                foreach (var processName in targetProcesses)
                {
                    Process[] processes = Process.GetProcessesByName(
                        Path.GetFileNameWithoutExtension(processName));

                    if (processes.Length > 0)
                    {
                        watchdogTimer.Stop();
                        onDetected?.Invoke();
                        return;
                    }
                }
            }
            catch
            {
                // Silenciar erros
            }
        }

        public void Start()
        {
            watchdogTimer.Start();
        }

        public void Stop()
        {
            watchdogTimer.Stop();
        }
    }

    public partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // Para renomear o processo na lista de tarefas
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleTitle(string lpConsoleTitle);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private ViGEmClient? client;
        private IDualShock4Controller? ds4;
        private System.Windows.Forms.Timer updateTimer = new();
        private System.Windows.Forms.Timer macroTimer = new();
        private FakerInputWrapper fakerInput = new FakerInputWrapper();
        private ProcessWatchdog watchdog;

        private Point lastMousePos;
        private Label statusLabel = new();
        private Label debugLabel = new();
        private TrackBar sensitivityXTrackBar = new();
        private TrackBar sensitivityYTrackBar = new();
        private TrackBar recenterDelayTrackBar = new();
        private CheckBox centerUntilMoveCheckBox = new();
        private CheckBox debugCheck = new();
        private ComboBox presetComboBox = new();
        private Button savePresetButton = new();
        private Button loadPresetButton = new();

        // Adicionar estas variáveis de classe logo após as variáveis existentes na classe MainForm
        private Queue<Point> mouseDeltaHistory = new Queue<Point>();
        private const int smoothingSamples = 5; // quantas amostras usar para suavização
        private float accelerationExponent = 1.2f; // ajuste de curva exponencial
        private float deadzoneThreshold = 1.5f; // limiar mínimo para enviar movimento ao analógico
        private byte lastRightAnalogX = 128;
        private byte lastRightAnalogY = 128;
        private TrackBar accelerationExponentTrackBar = new();
        private TrackBar deadzoneThresholdTrackBar = new();
        private TrackBar smoothingSamplesTrackBar = new();
        private CheckBox enableAdvancedMouseCheckBox = new();
        private bool enableAdvancedMouse = true;
        private Point screenCenter;
        private bool stealthMode = false; // Alterado de true para false
        private CheckBox stealthModeCheckBox = new();
        private CheckBox watchdogEnabledCheckBox = new();
        private bool watchdogEnabled = true;

        private ComboBox macroPresetComboBox = new ComboBox();
        private ComboBox macroSetComboBox = new ComboBox();
        private Button macroConfigButton = new Button();
        private CheckBox macroLoopCheckBox = new CheckBox();
        private Dictionary<string, List<(int keyCode, int hold, int wait)>> macroPresets = new();
        private Dictionary<DS4ButtonType, string> macroTriggers = new Dictionary<DS4ButtonType, string>();
        private Dictionary<string, Dictionary<DS4ButtonType, string>> macroSets = new Dictionary<string, Dictionary<DS4ButtonType, string>>();
        private string currentMacroSet = "Default";
        private string macroDirectory = "macros";
        private string macroConfigFile = "macro_config.xml";
        private string macroSetsFile = "macro_sets.xml";
        private bool isMacroRunning = false;
        private bool macroLoopEnabled = false;
        private int currentMacroStep = 0;
        private string currentRunningMacro = "";

        private float sensitivityX = 0.15f;
        private float sensitivityY = 0.15f;
        private int recenterDelay = 500;
        private bool centerUntilMove = false;
        private bool debugMode = true;

        private DateTime lastMouseMove;

        // Key mapping configuration
        private Dictionary<DS4ButtonType, Keys> buttonMappings = new Dictionary<DS4ButtonType, Keys>();
        private Dictionary<string, Dictionary<DS4ButtonType, Keys>> presets = new Dictionary<string, Dictionary<DS4ButtonType, Keys>>();
        private string currentPreset = "Default";
        private string configFilePath = "config.xml";

        // WASD to left analog mapping
        private bool leftAnalogEnabled = true;

        public MainForm()
        {
            this.BackColor = Color.Black;
            this.ForeColor = Color.Cyan;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            {
                SuspendLayout();
                // Usar strings ofuscadas para o título
                this.Text = "System Input Service";
                this.ClientSize = new Size(800, 600);
                InitializeButtonMappings();
                SetupUI();
                SetupMacroUI();
                SetupController();
                LoadPresets();
                LoadMacroConfig();
                LoadMacroSets();
                ResumeLayout(false);

                InitializeScreenCenter();
                // Não centralizar automaticamente
                // SetCursorPos(screenCenter.X, screenCenter.Y);

                // Inicializar watchdog
                watchdog = new ProcessWatchdog(() => {
                    if (watchdogEnabled)
                    {
                        // Auto-kill se detectar o jogo
                        Application.Exit();
                    }
                });

                if (watchdogEnabled)
                {
                    watchdog.Start();
                }

                AntiDetection.InitializeAntiDetection("ViGEmBus.sys", "wininput_helper64.dll");
                InputMasker.StartHook();
            }

            // Setup macro timer
            macroTimer.Interval = 10; // 10ms interval for macro execution
            macroTimer.Tick += MacroTimer_Tick;
        }

        private void MacroTimer_Tick(object sender, EventArgs e)
        {
            if (!isMacroRunning || string.IsNullOrEmpty(currentRunningMacro) || !macroPresets.ContainsKey(currentRunningMacro))
            {
                macroTimer.Stop();
                isMacroRunning = false;
                return;
            }

            var macroSteps = macroPresets[currentRunningMacro];
            if (macroSteps.Count == 0)
            {
                macroTimer.Stop();
                isMacroRunning = false;
                return;
            }

            // Execute current step
            if (currentMacroStep < macroSteps.Count)
            {
                var step = macroSteps[currentMacroStep];
                int keyCode = step.keyCode;
                int holdTime = step.hold;
                int waitTime = step.wait;

                // Press key
                keybd_event((byte)keyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);

                // Schedule key release after hold time
                System.Windows.Forms.Timer releaseTimer = new System.Windows.Forms.Timer();
                releaseTimer.Interval = holdTime;
                releaseTimer.Tick += (s, args) =>
                {
                    keybd_event((byte)keyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    releaseTimer.Stop();
                    releaseTimer.Dispose();
                };
                releaseTimer.Start();

                // Move to next step
                currentMacroStep++;

                // If this is the last step, check if we should loop
                if (currentMacroStep >= macroSteps.Count)
                {
                    if (macroLoopEnabled)
                    {
                        currentMacroStep = 0; // Reset to start for looping
                        macroTimer.Interval = waitTime > 0 ? waitTime : 10; // Wait before next loop
                    }
                    else
                    {
                        macroTimer.Stop();
                        isMacroRunning = false;
                    }
                }
                else
                {
                    // Set timer for next step
                    macroTimer.Interval = waitTime > 0 ? waitTime : 10;
                }
            }
        }

        private void InitializeButtonMappings()
        {
            // Default mappings
            // Standard buttons
            buttonMappings[DS4ButtonType.Cross] = Keys.Space;       // Jump
            buttonMappings[DS4ButtonType.Circle] = Keys.C;          // Crouch/Slide
            buttonMappings[DS4ButtonType.Square] = Keys.F;          // Use/Interact
            buttonMappings[DS4ButtonType.Triangle] = Keys.D1;       // Weapon Swap

            // D-Pad
            buttonMappings[DS4ButtonType.DPadUp] = Keys.D3;         // Killstreak 1
            buttonMappings[DS4ButtonType.DPadRight] = Keys.D4;      // Killstreak 2
            buttonMappings[DS4ButtonType.DPadDown] = Keys.V;        // Melee
            buttonMappings[DS4ButtonType.DPadLeft] = Keys.D5;       // Killstreak 3

            // Shoulder buttons
            buttonMappings[DS4ButtonType.ShoulderLeft] = Keys.Q;    // Tactical Equipment (L1)
            buttonMappings[DS4ButtonType.ShoulderRight] = Keys.E;   // Lethal Equipment (R1)

            // Trigger buttons (L2/R2 are handled through SetSliderValue)
            buttonMappings[DS4ButtonType.TriggerLeft] = Keys.ShiftKey;  // Aim Down Sights (L2)
            buttonMappings[DS4ButtonType.TriggerRight] = Keys.LButton;  // Fire Weapon (R2)

            // Thumb buttons
            buttonMappings[DS4ButtonType.ThumbLeft] = Keys.LControlKey; // Sprint/Tactical Sprint (L3)
            buttonMappings[DS4ButtonType.ThumbRight] = Keys.B;      // Melee/Finishing Move (R3)

            // Special buttons
            buttonMappings[DS4ButtonType.Share] = Keys.Tab;         // Scoreboard
            buttonMappings[DS4ButtonType.Options] = Keys.Escape;    // Menu
            buttonMappings[DS4ButtonType.PS] = Keys.Home;           // PS Button
            buttonMappings[DS4ButtonType.Touchpad] = Keys.M;        // Map

            // Create presets
            presets["Default"] = new Dictionary<DS4ButtonType, Keys>(buttonMappings);

            // Create a generic preset
            var genericPreset = new Dictionary<DS4ButtonType, Keys>
            {
                [DS4ButtonType.Cross] = Keys.Space,
                [DS4ButtonType.Circle] = Keys.C,
                [DS4ButtonType.Square] = Keys.E,
                [DS4ButtonType.Triangle] = Keys.Q,

                [DS4ButtonType.DPadUp] = Keys.D1,
                [DS4ButtonType.DPadRight] = Keys.D2,
                [DS4ButtonType.DPadDown] = Keys.D3,
                [DS4ButtonType.DPadLeft] = Keys.D4,

                [DS4ButtonType.ShoulderLeft] = Keys.R,
                [DS4ButtonType.ShoulderRight] = Keys.F,
                [DS4ButtonType.TriggerLeft] = Keys.ShiftKey,
                [DS4ButtonType.TriggerRight] = Keys.LButton,

                [DS4ButtonType.ThumbLeft] = Keys.LControlKey,
                [DS4ButtonType.ThumbRight] = Keys.V,

                [DS4ButtonType.Share] = Keys.Tab,
                [DS4ButtonType.Options] = Keys.Escape,
                [DS4ButtonType.PS] = Keys.Home,
                [DS4ButtonType.Touchpad] = Keys.M
            };
            presets["Generic"] = genericPreset;
        }

        private void SetupUI()
        {
            // Botão de ligar/desligar controle
            Button toggleControllerButton = new Button();
            toggleControllerButton.Text = "Ativar/Desativar Controle";
            toggleControllerButton.BackColor = Color.FromArgb(30, 30, 30);
            toggleControllerButton.ForeColor = Color.Aqua;
            toggleControllerButton.FlatStyle = FlatStyle.Flat;
            toggleControllerButton.Location = new Point(10, 12);
            toggleControllerButton.Size = new Size(200, 25);
            toggleControllerButton.Click += (s, e) => ToggleController();
            Controls.Add(toggleControllerButton);

            statusLabel.Location = new Point(10, 50);
            statusLabel.AutoSize = true;
            statusLabel.Text = "Iniciando...";
            Controls.Add(statusLabel);

            sensitivityXTrackBar.Location = new Point(10, 80);
            sensitivityXTrackBar.Size = new Size(300, 45);
            sensitivityXTrackBar.Minimum = 1;
            sensitivityXTrackBar.Maximum = 100;
            sensitivityXTrackBar.Value = 15;
            sensitivityXTrackBar.ValueChanged += (s, e) => sensitivityX = sensitivityXTrackBar.Value / 100f;
            Controls.Add(new Label() { Text = "Sensibilidade X", Location = new Point(10, 70), AutoSize = true, ForeColor = Color.DeepSkyBlue });
            Controls.Add(sensitivityXTrackBar);

            sensitivityYTrackBar.Location = new Point(10, 140);
            sensitivityYTrackBar.Size = new Size(300, 45);
            sensitivityYTrackBar.Minimum = 1;
            sensitivityYTrackBar.Maximum = 100;
            sensitivityYTrackBar.Value = 15;
            sensitivityYTrackBar.ValueChanged += (s, e) => sensitivityY = sensitivityYTrackBar.Value / 100f;
            Controls.Add(new Label() { Text = "Sensibilidade Y", Location = new Point(10, 130), AutoSize = true, ForeColor = Color.DeepSkyBlue });
            Controls.Add(sensitivityYTrackBar);

            recenterDelayTrackBar.Location = new Point(10, 200);
            recenterDelayTrackBar.Size = new Size(300, 45);
            recenterDelayTrackBar.Minimum = 0;
            recenterDelayTrackBar.Maximum = 1000;
            recenterDelayTrackBar.TickFrequency = 100;
            recenterDelayTrackBar.Value = recenterDelay;
            recenterDelayTrackBar.ValueChanged += (s, e) => {
                recenterDelay = recenterDelayTrackBar.Value;
                statusLabel.Text = $"Delay de recentralização: {recenterDelay}ms";
            };
            Controls.Add(new Label() { Text = "Delay Recentralização (ms)", Location = new Point(10, 190), AutoSize = true, ForeColor = Color.DeepSkyBlue });
            Controls.Add(recenterDelayTrackBar);

            centerUntilMoveCheckBox.Location = new Point(10, 260);
            centerUntilMoveCheckBox.Text = "Fixar ao centro";
            centerUntilMoveCheckBox.CheckedChanged += (s, e) => centerUntilMove = centerUntilMoveCheckBox.Checked;
            Controls.Add(centerUntilMoveCheckBox);

            debugCheck.Location = new Point(10, 290);
            debugCheck.Text = "Debug";
            debugCheck.Checked = debugMode;
            debugCheck.CheckedChanged += (s, e) => debugMode = debugCheck.Checked;
            Controls.Add(debugCheck);

            debugLabel.Location = new Point(10, 320);
            debugLabel.AutoSize = true;
            Controls.Add(debugLabel);

            // Preset selection
            Label presetLabel = new Label();
            presetLabel.Text = "Preset de Controle:";
            presetLabel.Location = new Point(350, 70);
            presetLabel.AutoSize = true;
            presetLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(presetLabel);

            presetComboBox.Location = new Point(350, 90);
            presetComboBox.Size = new Size(200, 25);
            presetComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            presetComboBox.BackColor = Color.FromArgb(30, 30, 30);
            presetComboBox.ForeColor = Color.Aqua;
            presetComboBox.Items.AddRange(new object[] { "Default", "Generic" });
            presetComboBox.SelectedIndex = 0;
            presetComboBox.SelectedIndexChanged += (s, e) => {
                if (presetComboBox.SelectedItem != null)
                {
                    currentPreset = presetComboBox.SelectedItem.ToString() ?? "Default";
                    if (presets.ContainsKey(currentPreset))
                    {
                        buttonMappings = new Dictionary<DS4ButtonType, Keys>(presets[currentPreset]);
                        statusLabel.Text = $"Preset carregado: {currentPreset}";
                    }
                }
            };
            Controls.Add(presetComboBox);

            // Button mapping UI
            Label mappingLabel = new Label();
            mappingLabel.Text = "Mapeamento de Botões:";
            mappingLabel.Location = new Point(350, 130);
            mappingLabel.AutoSize = true;
            mappingLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(mappingLabel);

            // Create button mapping panel
            Panel mappingPanel = new Panel();
            mappingPanel.Location = new Point(350, 150);
            mappingPanel.Size = new Size(430, 400);
            mappingPanel.AutoScroll = true;
            Controls.Add(mappingPanel);

            // Add button mapping controls
            int y = 10;
            foreach (DS4ButtonType buttonType in Enum.GetValues(typeof(DS4ButtonType)))
            {
                Label buttonLabel = new Label();
                buttonLabel.Text = buttonType.ToString();
                buttonLabel.Location = new Point(10, y);
                buttonLabel.Size = new Size(120, 20);
                buttonLabel.ForeColor = Color.White;
                mappingPanel.Controls.Add(buttonLabel);

                Button keyButton = new Button();
                keyButton.Text = buttonMappings.ContainsKey(buttonType) ? buttonMappings[buttonType].ToString() : "Não mapeado";
                keyButton.Location = new Point(140, y - 5);
                keyButton.Size = new Size(150, 25);
                keyButton.BackColor = Color.FromArgb(40, 40, 40);
                keyButton.ForeColor = Color.White;
                keyButton.FlatStyle = FlatStyle.Flat;
                keyButton.Tag = buttonType;
                keyButton.Click += KeyButton_Click;
                mappingPanel.Controls.Add(keyButton);

                y += 30;
            }

            // Save/Load preset buttons
            savePresetButton.Text = "Salvar Preset";
            savePresetButton.Location = new Point(350, 560);
            savePresetButton.Size = new Size(100, 30);
            savePresetButton.BackColor = Color.FromArgb(30, 30, 30);
            savePresetButton.ForeColor = Color.Aqua;
            savePresetButton.FlatStyle = FlatStyle.Flat;
            savePresetButton.Click += (s, e) => SavePresets();
            Controls.Add(savePresetButton);

            loadPresetButton.Text = "Carregar";
            loadPresetButton.Location = new Point(460, 560);
            loadPresetButton.Size = new Size(100, 30);
            loadPresetButton.BackColor = Color.FromArgb(30, 30, 30);
            loadPresetButton.ForeColor = Color.Aqua;
            loadPresetButton.FlatStyle = FlatStyle.Flat;
            loadPresetButton.Click += (s, e) => LoadPresets();
            Controls.Add(loadPresetButton);

            // Add new preset button
            Button newPresetButton = new Button();
            newPresetButton.Text = "Novo Preset";
            newPresetButton.Location = new Point(570, 560);
            newPresetButton.Size = new Size(100, 30);
            newPresetButton.BackColor = Color.FromArgb(30, 30, 30);
            newPresetButton.ForeColor = Color.Aqua;
            newPresetButton.FlatStyle = FlatStyle.Flat;
            newPresetButton.Click += (s, e) => {
                string presetName = Microsoft.VisualBasic.Interaction.InputBox("Digite o nome do novo preset:", "Novo Preset", "Custom");
                if (!string.IsNullOrEmpty(presetName) && !presets.ContainsKey(presetName))
                {
                    presets[presetName] = new Dictionary<DS4ButtonType, Keys>(buttonMappings);
                    presetComboBox.Items.Add(presetName);
                    presetComboBox.SelectedItem = presetName;
                    currentPreset = presetName;
                    statusLabel.Text = $"Novo preset criado: {presetName}";
                }
            };
            Controls.Add(newPresetButton);

            // Adicionar controles para os novos parâmetros de mouse avançado no método SetupUI
            // Adicione este código no final do método SetupUI, antes do fechamento da chave }

            // Configurações avançadas de mouse
            // Configurações avançadas de mouse
            Label advancedMouseLabel = new Label();
            advancedMouseLabel.Text = "Configurações Avançadas de Mouse:";
            advancedMouseLabel.Location = new Point(10, 360);
            advancedMouseLabel.AutoSize = true;
            advancedMouseLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(advancedMouseLabel);

            enableAdvancedMouseCheckBox.Location = new Point(10, 385);
            enableAdvancedMouseCheckBox.Text = "Habilitar";
            enableAdvancedMouseCheckBox.Checked = enableAdvancedMouse;
            enableAdvancedMouseCheckBox.CheckedChanged += (s, e) => enableAdvancedMouse = enableAdvancedMouseCheckBox.Checked;
            Controls.Add(enableAdvancedMouseCheckBox);

            stealthModeCheckBox.Location = new Point(100, 385);
            stealthModeCheckBox.Text = "Modo Stealth";
            stealthModeCheckBox.Checked = stealthMode;
            stealthModeCheckBox.CheckedChanged += (s, e) => stealthMode = stealthModeCheckBox.Checked;
            Controls.Add(stealthModeCheckBox);

            watchdogEnabledCheckBox.Location = new Point(220, 385);
            watchdogEnabledCheckBox.Text = "Auto-Kill";
            watchdogEnabledCheckBox.Checked = watchdogEnabled;
            watchdogEnabledCheckBox.CheckedChanged += (s, e) => {
                watchdogEnabled = watchdogEnabledCheckBox.Checked;
                if (watchdogEnabled)
                    watchdog.Start();
                else
                    watchdog.Stop();
            };
            Controls.Add(watchdogEnabledCheckBox);

            Label accelerationLabel = new Label();
            accelerationLabel.Text = "Curva de Aceleração:";
            accelerationLabel.Location = new Point(10, 415);
            accelerationLabel.AutoSize = true;
            accelerationLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(accelerationLabel);

            accelerationExponentTrackBar.Location = new Point(10, 425);
            accelerationExponentTrackBar.Size = new Size(300, 45);
            accelerationExponentTrackBar.Minimum = 5;
            accelerationExponentTrackBar.Maximum = 20;
            accelerationExponentTrackBar.Value = (int)(accelerationExponent * 10);
            accelerationExponentTrackBar.TickFrequency = 1;
            accelerationExponentTrackBar.ValueChanged += (s, e) => {
                accelerationExponent = accelerationExponentTrackBar.Value / 10f;
                statusLabel.Text = $"Curva de aceleração: {accelerationExponent:F1}";
            };
            Controls.Add(accelerationExponentTrackBar);

            Label deadzoneLabel = new Label();
            deadzoneLabel.Text = "Deadzone:";
            deadzoneLabel.Location = new Point(10, 470);
            deadzoneLabel.AutoSize = true;
            deadzoneLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(deadzoneLabel);

            deadzoneThresholdTrackBar.Location = new Point(10, 480);
            deadzoneThresholdTrackBar.Size = new Size(300, 45);
            deadzoneThresholdTrackBar.Minimum = 0;
            deadzoneThresholdTrackBar.Maximum = 50;
            deadzoneThresholdTrackBar.Value = (int)(deadzoneThreshold * 10);
            deadzoneThresholdTrackBar.TickFrequency = 5;
            deadzoneThresholdTrackBar.ValueChanged += (s, e) => {
                deadzoneThreshold = deadzoneThresholdTrackBar.Value / 10f;
                statusLabel.Text = $"Deadzone: {deadzoneThreshold:F1}";
            };
            Controls.Add(deadzoneThresholdTrackBar);

            Label smoothingLabel = new Label();
            smoothingLabel.Text = "Suavização:";
            smoothingLabel.Location = new Point(10, 525);
            smoothingLabel.AutoSize = true;
            smoothingLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(smoothingLabel);

            smoothingSamplesTrackBar.Location = new Point(10, 535);
            smoothingSamplesTrackBar.Size = new Size(300, 45);
            smoothingSamplesTrackBar.Minimum = 1;
            smoothingSamplesTrackBar.Maximum = 10;
            smoothingSamplesTrackBar.Value = smoothingSamples;
            smoothingSamplesTrackBar.TickFrequency = 1;
            smoothingSamplesTrackBar.ValueChanged += (s, e) => {
                mouseDeltaHistory.Clear();
                statusLabel.Text = $"Suavização: {smoothingSamplesTrackBar.Value} amostras";
            };
            Controls.Add(smoothingSamplesTrackBar);
        }

        private void SetupMacroUI()
        {
            if (!Directory.Exists(macroDirectory))
                Directory.CreateDirectory(macroDirectory);

            // Macro set selection
            Label macroSetLabel = new Label();
            macroSetLabel.Text = "Preset de Macros:";
            macroSetLabel.Location = new Point(220, 15);
            macroSetLabel.AutoSize = true;
            macroSetLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(macroSetLabel);

            macroSetComboBox.Location = new Point(330, 12);
            macroSetComboBox.Size = new Size(150, 25);
            macroSetComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            macroSetComboBox.BackColor = Color.FromArgb(30, 30, 30);
            macroSetComboBox.ForeColor = Color.Aqua;
            macroSetComboBox.Items.Add("Default");
            macroSetComboBox.SelectedIndex = 0;
            macroSetComboBox.SelectedIndexChanged += (s, e) => {
                if (macroSetComboBox.SelectedItem != null)
                {
                    string selectedSet = macroSetComboBox.SelectedItem.ToString();
                    if (macroSets.ContainsKey(selectedSet))
                    {
                        currentMacroSet = selectedSet;
                        macroTriggers = new Dictionary<DS4ButtonType, string>(macroSets[selectedSet]);
                        statusLabel.Text = $"Preset de macros carregado: {selectedSet}";
                    }
                }
            };
            Controls.Add(macroSetComboBox);

            // Novo preset de macros
            Button newMacroSetButton = new Button();
            newMacroSetButton.Text = "Novo Preset";
            newMacroSetButton.Location = new Point(490, 12);
            newMacroSetButton.Size = new Size(100, 25);
            newMacroSetButton.BackColor = Color.FromArgb(30, 30, 30);
            newMacroSetButton.ForeColor = Color.Aqua;
            newMacroSetButton.FlatStyle = FlatStyle.Flat;
            newMacroSetButton.Click += (s, e) => {
                string setName = Microsoft.VisualBasic.Interaction.InputBox("Digite o nome do novo preset de macros:", "Novo Preset de Macros", "");
                if (!string.IsNullOrWhiteSpace(setName) && !macroSets.ContainsKey(setName))
                {
                    macroSets[setName] = new Dictionary<DS4ButtonType, string>();
                    macroSetComboBox.Items.Add(setName);
                    macroSetComboBox.SelectedItem = setName;
                    currentMacroSet = setName;
                    macroTriggers = new Dictionary<DS4ButtonType, string>();
                    statusLabel.Text = $"Novo preset de macros criado: {setName}";
                    SaveMacroSets();
                }
            };
            Controls.Add(newMacroSetButton);

            // Macro loop checkbox
            macroLoopCheckBox = new CheckBox();
            macroLoopCheckBox.Text = "Loop";
            macroLoopCheckBox.Location = new Point(600, 15);
            macroLoopCheckBox.AutoSize = true;
            macroLoopCheckBox.ForeColor = Color.White;
            macroLoopCheckBox.CheckedChanged += (s, e) => macroLoopEnabled = macroLoopCheckBox.Checked;
            Controls.Add(macroLoopCheckBox);

            // Macro config button
            macroConfigButton.Text = "Configurar Macros";
            macroConfigButton.Location = new Point(660, 12);
            macroConfigButton.Size = new Size(130, 25);
            macroConfigButton.BackColor = Color.FromArgb(30, 30, 30);
            macroConfigButton.ForeColor = Color.Aqua;
            macroConfigButton.FlatStyle = FlatStyle.Flat;
            macroConfigButton.Click += (s, e) => new MacroEditorForm(macroDirectory, macroPresetComboBox, macroTriggers, currentMacroSet, this).Show();
            Controls.Add(macroConfigButton);

            LoadMacroPresets();
        }

        private void LoadMacroPresets()
        {
            macroPresetComboBox.Items.Clear();
            var files = Directory.GetFiles(macroDirectory, "*.txt");
            foreach (var file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                macroPresetComboBox.Items.Add(name);

                // Parse macro file
                try
                {
                    string[] lines = File.ReadAllLines(file);
                    List<(int keyCode, int hold, int wait)> macroSteps = new List<(int keyCode, int hold, int wait)>();

                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 3 && int.TryParse(parts[0], out int keyCode) &&
                            int.TryParse(parts[1], out int hold) && int.TryParse(parts[2], out int wait))
                        {
                            macroSteps.Add((keyCode, hold, wait));
                        }
                    }

                    macroPresets[name] = macroSteps;
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"Erro ao carregar macro {name}: {ex.Message}";
                }
            }
            if (macroPresetComboBox.Items.Count > 0)
                macroPresetComboBox.SelectedIndex = 0;
        }

        public void SaveMacroConfig()
        {
            try
            {
                var serializableMacroTriggers = new List<MacroTriggerMapping>();
                foreach (var trigger in macroTriggers)
                {
                    serializableMacroTriggers.Add(new MacroTriggerMapping
                    {
                        ButtonType = trigger.Key.ToString(),
                        MacroName = trigger.Value
                    });
                }

                XmlSerializer serializer = new XmlSerializer(typeof(List<MacroTriggerMapping>));
                using (TextWriter writer = new StreamWriter(macroConfigFile))
                {
                    serializer.Serialize(writer, serializableMacroTriggers);
                }

                statusLabel.Text = "Configuração de macros salva com sucesso!";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Erro ao salvar configuração de macros: {ex.Message}";
            }
        }

        private void LoadMacroConfig()
        {
            try
            {
                if (File.Exists(macroConfigFile))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<MacroTriggerMapping>));
                    List<MacroTriggerMapping> serializableMacroTriggers;

                    using (TextReader reader = new StreamReader(macroConfigFile))
                    {
                        serializableMacroTriggers = (List<MacroTriggerMapping>)serializer.Deserialize(reader);
                    }

                    macroTriggers.Clear();
                    foreach (var mapping in serializableMacroTriggers)
                    {
                        if (Enum.TryParse<DS4ButtonType>(mapping.ButtonType, out var buttonType))
                        {
                            macroTriggers[buttonType] = mapping.MacroName;
                        }
                    }

                    statusLabel.Text = "Configuração de macros carregada com sucesso!";
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Erro ao carregar configuração de macros: {ex.Message}";
            }
        }

        public void SaveMacroSets()
        {
            try
            {
                var serializableMacroSets = new List<MacroSetMapping>();

                foreach (var set in macroSets)
                {
                    var macroSet = new MacroSetMapping
                    {
                        SetName = set.Key,
                        MacroTriggers = new List<MacroTriggerMapping>()
                    };

                    foreach (var trigger in set.Value)
                    {
                        macroSet.MacroTriggers.Add(new MacroTriggerMapping
                        {
                            ButtonType = trigger.Key.ToString(),
                            MacroName = trigger.Value
                        });
                    }

                    serializableMacroSets.Add(macroSet);
                }

                XmlSerializer serializer = new XmlSerializer(typeof(List<MacroSetMapping>));
                using (TextWriter writer = new StreamWriter(macroSetsFile))
                {
                    serializer.Serialize(writer, serializableMacroSets);
                }

                statusLabel.Text = "Presets de macros salvos com sucesso!";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Erro ao salvar presets de macros: {ex.Message}";
            }
        }

        private void LoadMacroSets()
        {
            try
            {
                if (File.Exists(macroSetsFile))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<MacroSetMapping>));
                    List<MacroSetMapping> serializableMacroSets;

                    using (TextReader reader = new StreamReader(macroSetsFile))
                    {
                        serializableMacroSets = (List<MacroSetMapping>)serializer.Deserialize(reader);
                    }

                    macroSets.Clear();
                    macroSetComboBox.Items.Clear();
                    macroSetComboBox.Items.Add("Default");

                    foreach (var set in serializableMacroSets)
                    {
                        var triggerDict = new Dictionary<DS4ButtonType, string>();

                        foreach (var mapping in set.MacroTriggers)
                        {
                            if (Enum.TryParse<DS4ButtonType>(mapping.ButtonType, out var buttonType))
                            {
                                triggerDict[buttonType] = mapping.MacroName;
                            }
                        }

                        macroSets[set.SetName] = triggerDict;
                        macroSetComboBox.Items.Add(set.SetName);
                    }

                    // Select the first set or keep current if it exists
                    if (macroSetComboBox.Items.Count > 0)
                    {
                        if (macroSetComboBox.Items.Contains(currentMacroSet))
                            macroSetComboBox.SelectedItem = currentMacroSet;
                        else
                            macroSetComboBox.SelectedIndex = 0;
                    }

                    statusLabel.Text = "Presets de macros carregados com sucesso!";
                }
                else
                {
                    // Initialize with default set
                    macroSets["Default"] = new Dictionary<DS4ButtonType, string>();
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Erro ao carregar presets de macros: {ex.Message}";
                // Initialize with default set
                macroSets["Default"] = new Dictionary<DS4ButtonType, string>();
            }
        }

        private void ExecuteMacro(string macroName)
        {
            if (string.IsNullOrEmpty(macroName) || !macroPresets.ContainsKey(macroName) || isMacroRunning)
                return;

            currentRunningMacro = macroName;
            currentMacroStep = 0;
            isMacroRunning = true;
            macroTimer.Start();
            statusLabel.Text = $"Executando macro: {macroName}";
        }

        private void KeyButton_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            DS4ButtonType buttonType = (DS4ButtonType)btn.Tag;
            btn.Text = "Pressione uma tecla...";
            btn.Focus();

            // Store the event handler so we can remove it later
            KeyEventHandler handler = null;
            handler = (s, args) => {
                buttonMappings[buttonType] = args.KeyCode;
                btn.Text = args.KeyCode.ToString();

                // Update the current preset
                if (presets.ContainsKey(currentPreset))
                {
                    presets[currentPreset][buttonType] = args.KeyCode;
                }

                args.Handled = true;
                args.SuppressKeyPress = true;

                // Remove the event handler
                btn.KeyDown -= handler;
            };

            btn.KeyDown += handler;
        }

        private void SetupController()
        {
            if (client != null || ds4 != null) return;
            {
                try
                {
                    if (client != null || ds4 != null) return;

                    client = new ViGEmClient();
                    ds4 = client.CreateDualShock4Controller(0x054C, 0x09CC); // Sony DS4 v2
                    ds4.Connect();

                    updateTimer.Interval = 5;
                    updateTimer.Tick += UpdateLoop;
                    updateTimer.Start();
                    statusLabel.Text = "Controle emulado conectado.";

                    // Inicializa o cursor no centro da tela
                    SetCursorPos(screenCenter.X, screenCenter.Y);
                    GetCursorPos(out lastMousePos);
                    lastMouseMove = DateTime.Now;
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"Erro ao iniciar controle: {ex.Message}";
                }
            }
        }

        // Substituir o método UpdateLoop existente pelo método melhorado
        private void UpdateLoop(object? sender, EventArgs e)
        {
            // Toggle modo fixar ao centro (F12)
            if ((GetAsyncKeyState((int)Keys.F12) & 0x8000) != 0)
            {
                centerUntilMove = !centerUntilMove;
                centerUntilMoveCheckBox.Checked = centerUntilMove;
                Thread.Sleep(150);
            }

            // Toggle controle (F8)
            if ((GetAsyncKeyState((int)Keys.F8) & 0x8000) != 0)
            {
                ToggleController();
                Thread.Sleep(200);
            }

            if (ds4 == null || client == null) return;

            // Obter posição atual do cursor
            GetCursorPos(out Point currentPos);

            // Calcular delta em relação ao centro da tela (não ao último ponto)
            int dx = currentPos.X - screenCenter.X;
            int dy = currentPos.Y - screenCenter.Y;

            bool moved = dx != 0 || dy != 0;
            if (moved) lastMouseMove = DateTime.Now;

            // Resetar cursor para o centro da tela após coletar o delta
            if (stealthMode)
            {
                Console.WriteLine("STEALTH CURSOR RESET"); // Log de diagnóstico
                // SetCursorPos(screenCenter.X, screenCenter.Y); // Comentado para teste
            }

            // Right analog stick (mouse)
            byte rightAnalogX = 128;
            byte rightAnalogY = 128;

            if (enableAdvancedMouse)
            {
                // Adiciona o delta atual na fila
                Point delta = new(dx, dy);
                mouseDeltaHistory.Enqueue(delta);
                if (mouseDeltaHistory.Count > smoothingSamples)
                    mouseDeltaHistory.Dequeue();

                // Faz a média dos últimos deltas
                float avgX = (float)mouseDeltaHistory.Average(p => p.X);
                float avgY = (float)mouseDeltaHistory.Average(p => p.Y);

                // Curva de Aceleração Personalizada
                float accelX = Math.Sign(avgX) * (float)Math.Pow(Math.Abs(avgX), accelerationExponent);
                float accelY = Math.Sign(avgY) * (float)Math.Pow(Math.Abs(avgY), accelerationExponent);

                // Aplicar Sensibilidade E MAPEAR PRA RANGE 0-255 SEM ESTOURAR
                int analogX = 128 + (int)(accelX * sensitivityX * 10);
                int analogY = 128 + (int)(accelY * sensitivityY * 10);

                analogX = Math.Clamp(analogX, 0, 255);
                analogY = Math.Clamp(analogY, 0, 255);

                // Ignorar se movimento for MINÚSCULO (Deadzone, porra!)
                if (Math.Abs(accelX) < deadzoneThreshold) analogX = 128;
                if (Math.Abs(accelY) < deadzoneThreshold) analogY = 128;

                // Atualiza o controller
                rightAnalogX = (byte)analogX;
                rightAnalogY = (byte)analogY;

                lastRightAnalogX = rightAnalogX;
                lastRightAnalogY = rightAnalogY;
            }
            else
            {
                // Modo original mas baseado no delta do centro
                bool shouldRecenter = (DateTime.Now - lastMouseMove).TotalMilliseconds > recenterDelay;

                if (!shouldRecenter)
                {
                    rightAnalogX = (byte)Math.Clamp(128 + (int)(dx * sensitivityX * 10), 0, 255);
                    rightAnalogY = (byte)Math.Clamp(128 + (int)(dy * sensitivityY * 10), 0, 255);
                }
            }

            ds4.SetAxisValue(DualShock4Axis.RightThumbX, rightAnalogX);
            ds4.SetAxisValue(DualShock4Axis.RightThumbY, rightAnalogY);

            // Left analog stick (WASD)
            byte leftAnalogX = 128;
            byte leftAnalogY = 128;

            if (leftAnalogEnabled)
            {
                if ((GetAsyncKeyState((int)Keys.W) & 0x8000) != 0)
                    leftAnalogY = 0; // Up
                if ((GetAsyncKeyState((int)Keys.S) & 0x8000) != 0)
                    leftAnalogY = 255; // Down
                if ((GetAsyncKeyState((int)Keys.A) & 0x8000) != 0)
                    leftAnalogX = 0; // Left
                if ((GetAsyncKeyState((int)Keys.D) & 0x8000) != 0)
                    leftAnalogX = 255; // Right
            }

            ds4.SetAxisValue(DualShock4Axis.LeftThumbX, leftAnalogX);
            ds4.SetAxisValue(DualShock4Axis.LeftThumbY, leftAnalogY);

            // Dictionary to track button state changes for macro triggers
            Dictionary<DS4ButtonType, bool> buttonStates = new Dictionary<DS4ButtonType, bool>();

            // Process standard button mappings
            foreach (DS4ButtonType buttonType in Enum.GetValues(typeof(DS4ButtonType)))
            {
                bool isPressed = false;

                if (buttonMappings.ContainsKey(buttonType))
                {
                    isPressed = (GetAsyncKeyState((int)buttonMappings[buttonType]) & 0x8000) != 0;
                }

                buttonStates[buttonType] = isPressed;

                // Check if this button has a macro assigned
                if (macroTriggers.ContainsKey(buttonType) && isPressed && !isMacroRunning)
                {
                    string macroName = macroTriggers[buttonType];
                    if (!string.IsNullOrEmpty(macroName) && macroPresets.ContainsKey(macroName))
                    {
                        ExecuteMacro(macroName);
                    }
                }
            }

            // Set button states based on the dictionary
            ds4.SetButtonState(DualShock4Button.Cross, buttonStates[DS4ButtonType.Cross]);
            ds4.SetButtonState(DualShock4Button.Circle, buttonStates[DS4ButtonType.Circle]);
            ds4.SetButtonState(DualShock4Button.Square, buttonStates[DS4ButtonType.Square]);
            ds4.SetButtonState(DualShock4Button.Triangle, buttonStates[DS4ButtonType.Triangle]);
            ds4.SetButtonState(DualShock4Button.ShoulderLeft, buttonStates[DS4ButtonType.ShoulderLeft]);
            ds4.SetButtonState(DualShock4Button.ShoulderRight, buttonStates[DS4ButtonType.ShoulderRight]);
            ds4.SetButtonState(DualShock4Button.ThumbLeft, buttonStates[DS4ButtonType.ThumbLeft]);
            ds4.SetButtonState(DualShock4Button.ThumbRight, buttonStates[DS4ButtonType.ThumbRight]);
            ds4.SetButtonState(DualShock4Button.Share, buttonStates[DS4ButtonType.Share]);
            ds4.SetButtonState(DualShock4Button.Options, buttonStates[DS4ButtonType.Options]);

            // Handle D-Pad
            DualShock4DPadDirection dpadDirection = DualShock4DPadDirection.None;

            if (buttonStates[DS4ButtonType.DPadUp])
                dpadDirection = DualShock4DPadDirection.North;
            else if (buttonStates[DS4ButtonType.DPadRight])
                dpadDirection = DualShock4DPadDirection.East;
            else if (buttonStates[DS4ButtonType.DPadDown])
                dpadDirection = DualShock4DPadDirection.South;
            else if (buttonStates[DS4ButtonType.DPadLeft])
                dpadDirection = DualShock4DPadDirection.West;

            ds4.SetDPadDirection(dpadDirection);

            // Special case for triggers (L2/R2) - analog values
            if (buttonStates[DS4ButtonType.TriggerLeft])
            {
                ds4.SetButtonState(DualShock4Button.TriggerLeft, true);
                ds4.SetSliderValue(DualShock4Slider.LeftTrigger, 255);
            }
            else
            {
                ds4.SetButtonState(DualShock4Button.TriggerLeft, false);
                ds4.SetSliderValue(DualShock4Slider.LeftTrigger, 0);
            }

            if (buttonStates[DS4ButtonType.TriggerRight])
            {
                ds4.SetButtonState(DualShock4Button.TriggerRight, true);
                ds4.SetSliderValue(DualShock4Slider.RightTrigger, 255);
            }
            else
            {
                ds4.SetButtonState(DualShock4Button.TriggerRight, false);
                ds4.SetSliderValue(DualShock4Slider.RightTrigger, 0);
            }

            ds4.SubmitReport();

            if (debugMode)
            {
                string macroStatus = isMacroRunning ? $"Macro: {currentRunningMacro} (Passo {currentMacroStep})" : "";
                string mouseMode = enableAdvancedMouse ? "Avançado" : "Normal";
                string stealthStatus = stealthMode ? "Stealth ON" : "Stealth OFF";
                string watchdogStatus = watchdogEnabled ? "Auto-Kill ON" : "Auto-Kill OFF";
                debugLabel.Text = $"ΔX:{dx} ΔY:{dy} | {mouseMode} | {stealthStatus} | {watchdogStatus} | Preset: {currentPreset} | {macroStatus}";
            }
        }

        private void ToggleController()
        {
            if (ds4 != null && client != null)
            {
                ds4.Disconnect();
                client.Dispose();
                ds4 = null;
                client = null;
                statusLabel.Text = "Controle desligado.";
            }
            else
            {
                SetupController();
            }
        }

        private void SavePresets()
        {
            try
            {
                // Create a serializable dictionary
                var serializablePresets = new Dictionary<string, SerializableButtonMappingSet>();

                foreach (var preset in presets)
                {
                    var serializableSet = new SerializableButtonMappingSet();

                    // Convert button mappings
                    foreach (var mapping in preset.Value)
                    {
                        serializableSet.ButtonMappings.Add(new SerializableKeyMapping
                        {
                            ButtonType = mapping.Key.ToString(),
                            KeyValue = (int)mapping.Value
                        });
                    }

                    serializablePresets[preset.Key] = serializableSet;
                }

                // Serialize to XML
                XmlSerializer serializer = new XmlSerializer(typeof(Dictionary<string, SerializableButtonMappingSet>));
                using (TextWriter writer = new StreamWriter(configFilePath))
                {
                    serializer.Serialize(writer, serializablePresets);
                }

                statusLabel.Text = "Presets salvos com sucesso!";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Erro ao salvar presets: {ex.Message}";
            }
        }

        private void LoadPresets()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    // Deserialize from XML
                    XmlSerializer serializer = new XmlSerializer(typeof(Dictionary<string, SerializableButtonMappingSet>));
                    Dictionary<string, SerializableButtonMappingSet> serializablePresets;

                    using (TextReader reader = new StreamReader(configFilePath))
                    {
                        serializablePresets = (Dictionary<string, SerializableButtonMappingSet>)serializer.Deserialize(reader);
                    }

                    // Convert back to our format
                    presets.Clear();
                    presetComboBox.Items.Clear();

                    foreach (var preset in serializablePresets)
                    {
                        var buttonMappingDict = new Dictionary<DS4ButtonType, Keys>();

                        foreach (var mapping in preset.Value.ButtonMappings)
                        {
                            if (Enum.TryParse<DS4ButtonType>(mapping.ButtonType, out var buttonType))
                            {
                                buttonMappingDict[buttonType] = (Keys)mapping.KeyValue;
                            }
                        }

                        presets[preset.Key] = buttonMappingDict;
                        presetComboBox.Items.Add(preset.Key);
                    }

                    // Select the first preset or keep current if it exists
                    if (presetComboBox.Items.Count > 0)
                    {
                        if (presetComboBox.Items.Contains(currentPreset))
                            presetComboBox.SelectedItem = currentPreset;
                        else
                            presetComboBox.SelectedIndex = 0;
                    }

                    statusLabel.Text = "Presets carregados com sucesso!";
                }
                else
                {
                    statusLabel.Text = "Arquivo de configuração não encontrado.";
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Erro ao carregar presets: {ex.Message}";
            }
        }

        private void InitializeScreenCenter()
        {
            screenCenter = new Point(
                Screen.PrimaryScreen.Bounds.Width / 2,
                Screen.PrimaryScreen.Bounds.Height / 2
            );
        }

        private void EnableStealthMode()
        {
            // Não ativar automaticamente, apenas quando o usuário solicitar
            if (stealthMode)
            {
                try
                {
                    // Não podemos mudar Process.ProcessName diretamente, mas podemos mudar o título da janela
                    SetConsoleTitle("System Input Service");

                    // Bloquear tentativas de debug
                    System.Diagnostics.Debugger.Launch();

                    statusLabel.Text = "Modo stealth ativado";
                }
                catch
                {
                    // Silenciar erros em modo stealth
                }
            }
        }
    }

    public class MacroEditorForm : Form
    {
        private string macroDir;
        private ComboBox macroList = new ComboBox();
        private TextBox macroContent = new TextBox();
        private Button saveButton = new Button();
        private Button importButton = new Button();
        private Button newMacroButton = new Button();
        private Button testMacroButton = new Button();
        private ComboBox comboUpdate;
        private ComboBox triggerButtonComboBox = new ComboBox();
        private Button assignTriggerButton = new Button();
        private CheckBox loopCheckBox = new CheckBox();
        private Dictionary<DS4ButtonType, string> macroTriggers;
        private string currentMacroSet;
        private MainForm mainForm;

        public MacroEditorForm(string path, ComboBox macroCombo, Dictionary<DS4ButtonType, string> triggers, string macroSet, MainForm form)
        {
            macroDir = path;
            Text = "Editor de Macros";
            Size = new Size(700, 500);
            BackColor = Color.Black;
            ForeColor = Color.Cyan;
            Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            macroTriggers = triggers;
            currentMacroSet = macroSet;
            mainForm = form;

            // Macro list
            Label macroListLabel = new Label();
            macroListLabel.Text = "Selecione o Macro:";
            macroListLabel.Location = new Point(10, 10);
            macroListLabel.AutoSize = true;
            macroListLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(macroListLabel);

            macroList.Location = new Point(10, 30);
            macroList.Size = new Size(200, 25);
            macroList.DropDownStyle = ComboBoxStyle.DropDownList;
            macroList.BackColor = Color.FromArgb(30, 30, 30);
            macroList.ForeColor = Color.Aqua;
            macroList.SelectedIndexChanged += (s, e) => {
                if (macroList.SelectedItem != null)
                {
                    string filePath = Path.Combine(macroDir, macroList.SelectedItem + ".txt");
                    if (File.Exists(filePath))
                        macroContent.Text = File.ReadAllText(filePath);

                    // Update trigger button selection
                    UpdateTriggerButtonSelection();
                }
            };
            Controls.Add(macroList);

            // New macro button
            newMacroButton.Text = "Novo Macro";
            newMacroButton.Location = new Point(220, 30);
            newMacroButton.Size = new Size(100, 25);
            newMacroButton.BackColor = Color.FromArgb(30, 30, 30);
            newMacroButton.ForeColor = Color.Aqua;
            newMacroButton.FlatStyle = FlatStyle.Flat;
            newMacroButton.Click += (s, e) => {
                string macroName = Microsoft.VisualBasic.Interaction.InputBox("Digite o nome do novo macro:", "Novo Macro", "");
                if (!string.IsNullOrWhiteSpace(macroName))
                {
                    string filePath = Path.Combine(macroDir, macroName + ".txt");
                    if (!File.Exists(filePath))
                    {
                        File.WriteAllText(filePath, "");
                        LoadMacroList();
                        macroList.SelectedItem = macroName;
                        macroContent.Text = "";
                    }
                }
            };
            Controls.Add(newMacroButton);

            // Macro content
            Label macroContentLabel = new Label();
            macroContentLabel.Text = "Conteúdo do Macro (formato: keyCode,holdTime,waitTime):";
            macroContentLabel.Location = new Point(10, 60);
            macroContentLabel.AutoSize = true;
            macroContentLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(macroContentLabel);

            macroContent.Multiline = true;
            macroContent.Location = new Point(10, 80);
            macroContent.Size = new Size(660, 250);
            macroContent.BackColor = Color.FromArgb(30, 30, 30);
            macroContent.ForeColor = Color.White;
            Controls.Add(macroContent);

            // Trigger button selection
            Label triggerLabel = new Label();
            triggerLabel.Text = "Associar ao Botão:";
            triggerLabel.Location = new Point(10, 340);
            triggerLabel.AutoSize = true;
            triggerLabel.ForeColor = Color.DeepSkyBlue;
            Controls.Add(triggerLabel);

            triggerButtonComboBox.Location = new Point(10, 360);
            triggerButtonComboBox.Size = new Size(200, 25);
            triggerButtonComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            triggerButtonComboBox.BackColor = Color.FromArgb(30, 30, 30);
            triggerButtonComboBox.ForeColor = Color.Aqua;

            // Add all button types to the combo box
            foreach (DS4ButtonType buttonType in Enum.GetValues(typeof(DS4ButtonType)))
            {
                triggerButtonComboBox.Items.Add(buttonType.ToString());
            }

            Controls.Add(triggerButtonComboBox);

            // Assign trigger button
            assignTriggerButton.Text = "Associar";
            assignTriggerButton.Location = new Point(220, 360);
            assignTriggerButton.Size = new Size(100, 25);
            assignTriggerButton.BackColor = Color.FromArgb(30, 30, 30);
            assignTriggerButton.ForeColor = Color.Aqua;
            assignTriggerButton.FlatStyle = FlatStyle.Flat;
            assignTriggerButton.Click += (s, e) => {
                if (macroList.SelectedItem != null && triggerButtonComboBox.SelectedItem != null)
                {
                    string macroName = macroList.SelectedItem.ToString();
                    if (Enum.TryParse<DS4ButtonType>(triggerButtonComboBox.SelectedItem.ToString(), out var buttonType))
                    {
                        macroTriggers[buttonType] = macroName;
                        mainForm.SaveMacroConfig();
                        mainForm.SaveMacroSets();
                        MessageBox.Show($"Macro '{macroName}' associado ao botão {buttonType} no preset {currentMacroSet}!", "Associação Concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };
            Controls.Add(assignTriggerButton);

            // Loop checkbox
            loopCheckBox.Text = "Executar em Loop";
            loopCheckBox.Location = new Point(340, 360);
            loopCheckBox.AutoSize = true;
            loopCheckBox.ForeColor = Color.White;
            Controls.Add(loopCheckBox);

            // Test macro button
            testMacroButton.Text = "Testar Macro";
            testMacroButton.Location = new Point(480, 360);
            testMacroButton.Size = new Size(100, 25);
            testMacroButton.BackColor = Color.FromArgb(30, 30, 30);
            testMacroButton.ForeColor = Color.Aqua;
            testMacroButton.FlatStyle = FlatStyle.Flat;
            testMacroButton.Click += (s, e) => {
                if (macroList.SelectedItem != null)
                {
                    MessageBox.Show("Função de teste não implementada ainda.", "Informação", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            Controls.Add(testMacroButton);

            // Save button
            saveButton.Text = "Salvar Macro";
            saveButton.Location = new Point(10, 400);
            saveButton.Size = new Size(150, 30);
            saveButton.BackColor = Color.FromArgb(30, 30, 30);
            saveButton.ForeColor = Color.Aqua;
            saveButton.FlatStyle = FlatStyle.Flat;
            saveButton.Click += (s, e) => {
                if (macroList.SelectedItem != null)
                {
                    string name = macroList.SelectedItem.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        File.WriteAllText(Path.Combine(macroDir, name + ".txt"), macroContent.Text);
                        MessageBox.Show($"Macro '{name}' salvo com sucesso!", "Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };
            Controls.Add(saveButton);

            // Import button
            importButton.Text = "Importar Arquivo...";
            importButton.Location = new Point(170, 400);
            importButton.Size = new Size(150, 30);
            importButton.BackColor = Color.FromArgb(30, 30, 30);
            importButton.ForeColor = Color.Aqua;
            importButton.FlatStyle = FlatStyle.Flat;
            importButton.Click += (s, e) => {
                using OpenFileDialog ofd = new();
                ofd.Filter = "Arquivos de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string dest = Path.Combine(macroDir, Path.GetFileName(ofd.FileName));
                    File.Copy(ofd.FileName, dest, true);
                    LoadMacroList();
                    macroList.SelectedItem = Path.GetFileNameWithoutExtension(ofd.FileName);
                }
            };
            Controls.Add(importButton);

            // Help button
            Button helpButton = new Button();
            helpButton.Text = "Ajuda";
            helpButton.Location = new Point(570, 400);
            helpButton.Size = new Size(100, 30);
            helpButton.BackColor = Color.FromArgb(30, 30, 30);
            helpButton.ForeColor = Color.Aqua;
            helpButton.FlatStyle = FlatStyle.Flat;
            helpButton.Click += (s, e) => {
                string helpText = "Formato do Macro:\n\n" +
                                 "Cada linha representa um passo do macro no formato:\n" +
                                 "keyCode,holdTime,waitTime\n\n" +
                                 "keyCode: Código da tecla (ex: 65 para 'A')\n" +
                                 "holdTime: Tempo em ms para segurar a tecla\n" +
                                 "waitTime: Tempo em ms para esperar antes do próximo passo\n\n" +
                                 "Exemplo:\n" +
                                 "65,100,200 (Pressiona 'A' por 100ms, espera 200ms)\n" +
                                 "66,50,100 (Pressiona 'B' por 50ms, espera 100ms)";
                MessageBox.Show(helpText, "Ajuda do Editor de Macros", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Controls.Add(helpButton);

            // Current macro set label
            Label currentSetLabel = new Label();
            currentSetLabel.Text = $"Preset Atual: {currentMacroSet}";
            currentSetLabel.Location = new Point(340, 30);
            currentSetLabel.AutoSize = true;
            currentSetLabel.ForeColor = Color.Yellow;
            Controls.Add(currentSetLabel);

            comboUpdate = macroCombo;
            LoadMacroList();
            UpdateTriggerButtonSelection();
        }

        private void LoadMacroList()
        {
            macroList.Items.Clear();
            var files = Directory.GetFiles(macroDir, "*.txt");
            foreach (var file in files)
                macroList.Items.Add(Path.GetFileNameWithoutExtension(file));
            if (macroList.Items.Count > 0)
                macroList.SelectedIndex = 0;
            comboUpdate.Items.Clear();
            foreach (string item in macroList.Items)
                comboUpdate.Items.Add(item);
        }

        private void UpdateTriggerButtonSelection()
        {
            if (macroList.SelectedItem == null) return;

            string selectedMacro = macroList.SelectedItem.ToString();

            // Find which button is assigned to this macro
            foreach (var trigger in macroTriggers)
            {
                if (trigger.Value == selectedMacro)
                {
                    triggerButtonComboBox.SelectedItem = trigger.Key.ToString();
                    return;
                }
            }

            // If no button is assigned, clear selection
            triggerButtonComboBox.SelectedIndex = -1;
        }
    }

    // Serializable classes for XML serialization
    [Serializable]
    public class SerializableButtonMappingSet
    {
        public List<SerializableKeyMapping> ButtonMappings { get; set; } = new List<SerializableKeyMapping>();
    }

    [Serializable]
    public class SerializableKeyMapping
    {
        public string ButtonType { get; set; } = "";
        public int KeyValue { get; set; }
    }

    [Serializable]
    public class MacroTriggerMapping
    {
        public string ButtonType { get; set; } = "";
        public string MacroName { get; set; } = "";
    }

    [Serializable]
    public class MacroSetMapping
    {
        public string SetName { get; set; } = "";
        public List<MacroTriggerMapping> MacroTriggers { get; set; } = new List<MacroTriggerMapping>();
    }
}

// Adicione isso ao arquivo AssemblyInfo.cs
/*
[assembly: AssemblyTitle("System Input Service")]
[assembly: AssemblyDescription("Windows System Input Service Manager")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Windows System Services")]
[assembly: AssemblyCopyright("© Microsoft Corporation. All rights reserved.")]
[assembly: AssemblyTrademark("Microsoft® is a registered trademark of Microsoft Corporation.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
*/
