using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Timers;

namespace CheckAndClean
{
    public class MainForm : Form
    {
        private int postponeCount = 0;
        private const int maxPostpone = 3;
        private readonly string postponeFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "PostponeCount.txt");
        private Label countdownLabel = new(); // Initialize directly
        private Label messageLabel = new(); // Initialize directly
        private System.Timers.Timer countdownTimer = new(1000); // Initialize directly
        private DateTime targetTime;
        private bool isPostponed = false; // Flag to indicate if the window is being closed due to a postpone action
        private bool isConfirmed = false; // Flag to indicate if the window is being closed due to an OK action

        public MainForm()
        {
            InitializeComponent();
            postponeCount = GetPostponeCount();
            CleanUpTempTask(); // Clean up the temporary task if it exists
            UpdateCountdown();
            UpdateMessageLabel(); // Update the message label with the correct postpone count
        }

        private void InitializeComponent()
        {
            this.Text = "C Drive Clean Up";
            this.Width = 300;
            this.Height = 200;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true; // Make the window topmost

            // Set the window position to 45 pixels off from the bottom
            var primaryScreen = Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                this.Left = primaryScreen.WorkingArea.Width - this.Width - 10;
                this.Top = primaryScreen.WorkingArea.Height - this.Height - 30;
            }

            messageLabel.Width = 280;
            messageLabel.Height = 80;
            messageLabel.Top = 20;
            messageLabel.Left = 10;
            messageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.Controls.Add(messageLabel);

            countdownLabel.Width = 280;
            countdownLabel.Height = 20;
            countdownLabel.Top = 100;
            countdownLabel.Left = 10;
            countdownLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.Controls.Add(countdownLabel);

            Button postponeButton = new()
            {
                Text = "Postpone",
                Top = 130,
                Left = 50
            };
            postponeButton.Click += new EventHandler(PostponeButton_Click);
            this.Controls.Add(postponeButton);

            Button okButton = new()
            {
                Text = "OK",
                Top = 130,
                Left = 150
            };
            okButton.Click += new EventHandler(OkButton_Click);
            this.Controls.Add(okButton);

            countdownTimer.Elapsed += OnTimedEvent;
            countdownTimer.AutoReset = true;
            countdownTimer.Enabled = true;

            this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing); // Handle form closing event
        }

        private void PostponeButton_Click(object? sender, EventArgs e)
        {
            if (postponeCount < maxPostpone)
            {
                postponeCount++;
                SetPostponeCount(postponeCount);
                MessageBox.Show("The cleanup has been postponed by 1 hour.");

                // Create a new temporary task to trigger the original task in 1 hour
                string originalTaskName = $"{Environment.UserName}_CleanUpTask";
                string tempTaskName = $"{Environment.UserName}_TempCleanUpTask";

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks",
                        Arguments = $"/Create /TN \"{tempTaskName}\" /TR \"schtasks /Run /TN \\\"{originalTaskName}\\\"\" /SC ONCE /ST {DateTime.Now.AddHours(1):HH:mm} /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(startInfo);
                }
                catch
                {
                    Console.WriteLine("Error creating temporary task.");
                }

                isPostponed = true; // Set the flag to indicate the window is being closed due to a postpone action
                this.Close();
            }
            else
            {
                MessageBox.Show("You have reached the maximum number of postpones.");
            }
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("The cleanup is starting now.", "Cleanup", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            PerformCleanup();

            isConfirmed = true; // Set the flag to indicate the window is being closed due to an OK action
            this.Close();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!isPostponed && !isConfirmed)
            {
                // Postpone by 1 minute if the window is closed without clicking a button
                string originalTaskName = $"{Environment.UserName}_CleanUpTask";
                string tempTaskName = $"{Environment.UserName}_TempCleanUpTask";

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks",
                        Arguments = $"/Create /TN \"{tempTaskName}\" /TR \"schtasks /Run /TN \\\"{originalTaskName}\\\"\" /SC ONCE /ST {DateTime.Now.AddMinutes(1):HH:mm} /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(startInfo);
                }
                catch
                {
                    Console.WriteLine("Error creating temporary task.");
                }
            }
        }

        private bool ShouldRunCleanup()
        {
            return IsFirstSpecifiedDay(DayOfWeek.Monday) || IsOnDemand();
        }

        private bool IsFirstSpecifiedDay(DayOfWeek dayOfWeek)
        {
            DateTime today = DateTime.Today;
            DateTime firstSpecifiedDay = new(today.Year, today.Month, 1);

            while (firstSpecifiedDay.DayOfWeek != dayOfWeek)
            {
                firstSpecifiedDay = firstSpecifiedDay.AddDays(1);
            }

            return today == firstSpecifiedDay;
        }

        private bool IsOnDemand()
        {
            string taskName = $"{Environment.UserName}_CleanUpTask";
            try
            {
                Process process = new();
                process.StartInfo.FileName = "schtasks";
                process.StartInfo.Arguments = $"/Query /TN \"{taskName}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("Ready") || output.Contains("Running");
            }
            catch
            {
                Console.WriteLine("Error querying scheduled task.");
            }
            return false;
        }

        private int GetPostponeCount()
        {
            if (File.Exists(postponeFilePath))
            {
                return int.Parse(File.ReadAllText(postponeFilePath));
            }
            return 0;
        }

        private void SetPostponeCount(int count)
        {
            File.WriteAllText(postponeFilePath, count.ToString());
            File.SetAttributes(postponeFilePath, File.GetAttributes(postponeFilePath) | FileAttributes.Hidden);
        }

        private void PerformCleanup()
        {
            //TODO Uncomment
            // // Clean Downloads folder
            // string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            // foreach (var directory in Directory.GetDirectories(downloadsPath))
            // {
            //     Directory.Delete(directory, true);
            // }
            // foreach (var file in Directory.GetFiles(downloadsPath))
            // {
            //     File.Delete(file);
            // }

            // Clean current user's Recycle Bin using PowerShell
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processStartInfo))
            {
                if (process != null)
                {
                    process.WaitForExit();
                }
                else
                {
                    Console.WriteLine("Failed to start the process.");
                }
            }

            // Reset postpone count
            SetPostponeCount(0);
        }

        private void UpdateCountdown()
        {
            //TODO Change to correct time
            targetTime = DateTime.Now.AddSeconds(10); // Set the countdown target time to 10 minutes from now
            countdownTimer.Start();
        }

        private void OnTimedEvent(object? source, ElapsedEventArgs e)
        {
            TimeSpan remainingTime = targetTime - DateTime.Now;
            if (remainingTime.TotalSeconds > 0)
            {
                countdownLabel?.Invoke((MethodInvoker)delegate
                {
                    countdownLabel.Text = $"Time remaining: {remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
                });
            }
            else
            {
                countdownTimer.Stop();
                countdownLabel?.Invoke((MethodInvoker)delegate
                {
                    countdownLabel.Text = "Time's up!";
                });
                MessageBox.Show("The cleanup is starting now.", "Cleanup", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                PerformCleanup();

                isConfirmed = true; // Set the flag to indicate the window is being closed due to an OK action
                this.Close();
            }
        }

        private void CleanUpTempTask()
        {
            string tempTaskName = $"{Environment.UserName}_TempCleanUpTask";
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Delete /TN \"{tempTaskName}\" /F",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(startInfo);
            }
            catch
            {
                Console.WriteLine("Error deleting temporary task.");
            }
        }

        private void UpdateMessageLabel()
        {
            messageLabel.Text = $"Your scheduled cleanup will start soon.\nClick 'Postpone' to delay by 1 hour or 'OK' to start now.\n\nPostpones left: {maxPostpone - postponeCount}";
        }
    }
}