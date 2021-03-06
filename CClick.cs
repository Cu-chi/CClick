using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Timers;
using CClick.SettingsMenu;
using CClick.StatsMenu.StatsForm;
using System.Net;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using CClick.BestScoresMenu;

namespace CClick
{
    public partial class CClick : Form
    {
        #region Default values which are gonna be modified on test launched

        private bool isTestRunning = false;
        private int clickCounter = 0;
        private int battleModeHealth = 0;
        private int battleModeDamage = 0;
        private int typeModeEnteredValue = 0;
        private bool firstClickValuesCheck = false;
        private bool maximumProgressBarChecked = false;
        private string currentTestName = "";

        #endregion

        private readonly string version = "1.069";
        private System.Timers.Timer timer;
        private System.Media.SoundPlayer player = new System.Media.SoundPlayer(@"audio\click.wav");
        private Stopwatch watcher = new Stopwatch();
        private Json jData = new Json();
        private Data userData = new Data();
        private StatsData userStatsData = new StatsData();
        private SettingsForm settingsForm = new SettingsForm();
        private StatsForm statsForm = new StatsForm();
        private MenuBestScores bestScoresForm = new MenuBestScores();
        private SettingsFormUtilities settingsFormUtilities = new SettingsFormUtilities();

        public CClick()
        {
            InitializeComponent();

            #region Check if CClick is already started
            Process currentProcess = Process.GetCurrentProcess();
            Process[] procColl = Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (Process p in procColl)
            {
                if (p.Id != currentProcess.Id)
                {
                    MessageBox.Show("CClick is already opened", "CClick", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Close();
                }
            }
            #endregion

            #region Version Check
            string latestVersion = new WebClient().DownloadString("https://pastebin.com/raw/vNb10fgb");

            NumberFormatInfo provider = new NumberFormatInfo
            {
                NumberDecimalSeparator = "."
            };

            // Version check
            if (Convert.ToDouble(version, provider) < Convert.ToDouble(latestVersion, provider))
            {
                DialogResult verResult = MessageBox.Show($"A new version is now available :\nYou are using > {version}\nNew > {latestVersion}\nDo you want to install the new version?", "WARNING", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);

                if (verResult == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start https://github.com/Cu-chi/CClick/releases/download/{latestVersion}/CClick-{latestVersion}.zip") { CreateNoWindow = true });
                }
            }
            #endregion

            #region JSON: user settings data (default test, sound effect etc...)
            if (!System.IO.File.Exists("data\\data.json"))
            {
                DialogResult resultMsgBox = MessageBox.Show("Have you just updated Click?", "CClick", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                string olderDataCClickPath = "";
                if (resultMsgBox == DialogResult.Yes)
                {
                    FolderBrowserDialog openFolderDialog = new FolderBrowserDialog
                    {
                        Description = "Select your older CClick folder"
                    };

                    if (openFolderDialog.ShowDialog() == DialogResult.OK)
                    {
                        olderDataCClickPath = openFolderDialog.SelectedPath + "\\data";

                        DirectoryInfo dir = new DirectoryInfo(olderDataCClickPath);
                        FileInfo[] files;
                        try
                        {
                            files = dir.GetFiles();
                        }
                        catch (DirectoryNotFoundException)
                        {
                            MessageBox.Show("Can't find any data folder.", "CClick", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        Directory.CreateDirectory("data");

                        foreach (FileInfo file in files)
                        {
                            if (file.Extension == ".json")
                            {
                                file.CopyTo($"data\\{file.Name}", false);
                            }
                        }
                    }

                    updateLocalData();
                }
                else
                {
                    Data json = new Data
                    {
                        EnableSound = false,
                        EnableProgressBar = false,
                        DefaultTest = -1,
                        CustomConfig = null,
                        RichTextBoxSaveEnabled = false,
                        RichTextBoxContent = {},
                        SaveBestScoreForEachTest = true
                    };

                    Directory.CreateDirectory("data");

                    if (!jData.WriteData(json, "data\\data.json"))
                    {
                        MessageBox.Show("An error occured: Json file can't be created.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                        return;
                    }
                }
            }
            else
            {
                userData = jData.ReadData("data\\data.json");
                if (userData == null)
                {
                    MessageBox.Show("An error occured: Json file is not working.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }

                if (userData.EnableSound)
                    settingsForm.soundEffectCheckBox.CheckState = CheckState.Checked;

                if (userData.DefaultTest != -1)
                    comboBox.SelectedIndex = userData.DefaultTest;

                if (comboBox.SelectedIndex == 5) // If selected test is custom
                {
                    if (userData.CustomConfig != null)
                    {
                        if (userData.CustomConfig[0] == "click")
                            typeCheckBox.CheckState = CheckState.Unchecked;
                        else
                            typeCheckBox.CheckState = CheckState.Checked;

                        if (userData.CustomConfig[1] == "battle")
                            battleTypeCheckBox.CheckState = CheckState.Checked;
                        else
                            typeCheckBox.CheckState = CheckState.Unchecked;

                        typeTextBox.Text = userData.CustomConfig[2];
                        battleHealthTextBox.Text = userData.CustomConfig[3];
                        battleDamageTextBox.Text = userData.CustomConfig[4];
                    }

                    CustomTestDisplay(true); // Display customization panel
                }
            }
            #endregion

            #region JSON: user statistics data
            if (!System.IO.File.Exists("data\\stats.json"))
            {
                StatsData json = new StatsData
                {
                    ClicksAverage = 0,
                    TotalClicks = 0,
                    TotalMsElapsedOnTest = 0,
                    TotalTests = 0,
                    ClicksPerSecondsAverage = 0,
                    ClicksPerSecondsAllTest = {},
                    BestScores = null
                };

                if (!jData.WriteStatsData(json, "data\\stats.json"))
                {
                    MessageBox.Show("An error occured: Json file can't be created.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }
            }
            else
            {
                userStatsData = jData.ReadStatsData("data\\stats.json");
                if (userStatsData == null)
                {
                    MessageBox.Show("An error occured: Json file (stats) is not working.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }
            }
            #endregion
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ToolTip ToolTips = new ToolTip();
            ToolTips.SetToolTip(statsButton, "Open Statistics");
            ToolTips.SetToolTip(settingsIcon, "Open Settings");

            settingsForm.soundEffectCheckBox.CheckedChanged += new EventHandler(SoundEffectCheckedChanged);
            settingsForm.saveButton.Click += new EventHandler(saveButton_Click);
            settingsForm.saveLogsCheckBox.CheckedChanged += new EventHandler(SaveLogsCheckedChanged);
            settingsForm.resetButton.Click += new EventHandler(ResetDataFolder);
            settingsForm.resetStatsButton.Click += new EventHandler(ResetStatistics);
            settingsForm.progressBarCheckbox.CheckedChanged += new EventHandler(ProgressBarCheckedChanged);
            settingsForm.saveBestCheckBox.CheckedChanged += new EventHandler(SaveBestScoreCheckedChanged);

            if (userData.RichTextBoxSaveEnabled) // If user enabled the richtextbox save
            {
                Cursor.Current = Cursors.WaitCursor;
                richTextBox1.Text = userData.RichTextBoxContent; // Load the save into the richtextbox
                Cursor.Current = Cursors.Default;
            }
        }

        private void clickableButton_Click(object sender, EventArgs e)
        {
            if (settingsForm.soundEffectCheckBox.Checked)
            {
                try
                {
                    player.Play();
                }
                catch (System.IO.FileNotFoundException)
                {
                    MessageBox.Show($"FileNotFoundException: Can't find audio file.\nUnchecked sound effect to prevent errors.", "WARNING", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    settingsForm.soundEffectCheckBox.CheckState = CheckState.Unchecked;
                }
            }

            if (!isTestRunning)
            {
                if (comboBox.SelectedIndex != -1)
                    SetupOnNewRun();
                else
                    MessageBox.Show("Please, select a test!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                if (comboBox.SelectedIndex != -1)
                {
                    if (comboBox.SelectedIndex == 0) // 10 Clicks
                    {
                        if (clickCounter < 10)
                        {
                            clickCounter++;
                            clickLabel.Text = 10 - clickCounter + " left";
                            if (clickCounter == 10)
                            {
                                string s = "You took " + Math.Round(EndTest().TotalSeconds, 3).ToString() + "s to do 10 clicks\n";
                                richTextBox1.Text += s;
                                AddRichTextBoxSaveContent(s);
                                MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    else if (comboBox.SelectedIndex == 1) // 100 clicks
                    {
                        if (clickCounter < 100)
                        {
                            clickCounter++;
                            clickLabel.Text = 100 - clickCounter + " left";
                            if (clickCounter == 100)
                            {
                                string s = "You took " + Math.Round(EndTest().TotalSeconds, 3).ToString() + "s to do 100 clicks\n";
                                richTextBox1.Text += s;
                                AddRichTextBoxSaveContent(s);
                                MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    else if (comboBox.SelectedIndex == 2) // 1000 clicks
                    {
                        if (clickCounter < 1000)
                        {
                            clickCounter++;
                            clickLabel.Text = 1000 - clickCounter + " left";
                            if (clickCounter == 1000)
                            {
                                string s = "You took " + Math.Round(EndTest().TotalSeconds, 3).ToString() + "s to do 1000 clicks\n";
                                richTextBox1.Text += s;
                                AddRichTextBoxSaveContent(s);
                                MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    else if (comboBox.SelectedIndex == 3 || comboBox.SelectedIndex == 4) // Test using time
                    {
                        clickCounter++;
                        clickLabel.Text = clickCounter + " clicks";
                    }
                    else if (comboBox.SelectedIndex == 5) // Custom test
                    {
                        if (typeCheckBox.Checked)
                        {
                            clickCounter++;
                            clickLabel.Text = clickCounter + " clicks";
                        }
                        else if ((!typeCheckBox.Checked) && (!battleTypeCheckBox.Checked))
                        {
                            if (!firstClickValuesCheck)
                            {
                                typeModeEnteredValue = int.Parse(typeTextBox.Text);
                            }

                            if (clickCounter < typeModeEnteredValue)
                            {
                                clickCounter++;
                                clickLabel.Text = typeModeEnteredValue - clickCounter + " left";
                                if (clickCounter == typeModeEnteredValue)
                                {
                                    string s = "You took " + Math.Round(EndTest().TotalSeconds, 3).ToString() + $"s to do {typeModeEnteredValue} clicks\n";
                                    richTextBox1.Text += s;
                                    AddRichTextBoxSaveContent(s);
                                    MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                        }
                        else if (battleTypeCheckBox.Checked)
                        {
                            battleModeHealth -= battleModeDamage;
                            clickLabel.Text = battleModeHealth + "hp remaining";
                            if (battleModeHealth <= 0)
                            {
                                string s = "You took " + Math.Round(EndTest().TotalSeconds, 3).ToString() + $"s to do kill enemy with {battleHealthTextBox.Text}hp, you were doing {battleModeDamage} damage per click\n";
                                richTextBox1.Text += s;
                                AddRichTextBoxSaveContent(s);
                                MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Please, select a test!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                progressBar.Invoke(new Action(() => {
                    switch (comboBox.SelectedIndex)
                    {
                        case 0: // If 10 clicks test
                        if (!maximumProgressBarChecked)
                        {
                            progressBar.Maximum = 10;
                            maximumProgressBarChecked = true;
                        }
                        progressBar.Value = clickCounter;
                        break;
                        case 1: // If 100 clicks test
                        if (!maximumProgressBarChecked)
                        {
                            progressBar.Maximum = 100;
                            maximumProgressBarChecked = true;
                        }
                        progressBar.Value = clickCounter;
                        break;
                        case 2: // If 1000 clicks test
                        if (!maximumProgressBarChecked)
                        {
                            progressBar.Maximum = 1000;
                            maximumProgressBarChecked = true;
                        }
                        progressBar.Value = clickCounter;
                        break;
                        case 3: // If 10 sec test
                        if (!maximumProgressBarChecked)
                        {
                            progressBar.Maximum = 10;
                            maximumProgressBarChecked = true;
                        }
                        progressBar.Value = (int)watcher.Elapsed.TotalSeconds;
                        break;
                        case 4: // If 1 sec test
                        if (!maximumProgressBarChecked)
                        {
                            progressBar.Maximum = 1;
                            maximumProgressBarChecked = true;
                        }
                        progressBar.Value = (int)watcher.Elapsed.TotalSeconds;
                        break;
                        case 5: // If is a custom test
                        if (typeCheckBox.Checked)
                        {
                            if (!maximumProgressBarChecked)
                            {
                                progressBar.Maximum = int.Parse(typeTextBox.Text);
                                maximumProgressBarChecked = true;
                            }
                            progressBar.Value = (int)watcher.Elapsed.TotalSeconds;
                        }
                        else if ((!typeCheckBox.Checked) && (!battleTypeCheckBox.Checked))
                        {
                            if (!maximumProgressBarChecked)
                            {
                                progressBar.Maximum = typeModeEnteredValue;
                                maximumProgressBarChecked = true;
                            }
                            progressBar.Value = clickCounter;
                        }
                        else if (battleTypeCheckBox.Checked)
                        {
                            if (!maximumProgressBarChecked)
                            {
                                progressBar.Maximum = battleModeHealth;
                                maximumProgressBarChecked = true;
                            }
                            progressBar.Value = progressBar.Maximum - battleModeHealth;
                        }
                        break;
                    }
                }));
            }
        }

        private void SetupOnNewRun()
        {
            #region Changing / removing / creating vars
            clickableButton.Text = "CLICK";
            clickCounter = 1;
            isTestRunning = true;
            watcher.Reset();
            watcher.Start();
            timer = new System.Timers.Timer();
            timer.Elapsed += Timer;
            timer.Enabled = true;
            progressBar.Visible = userData.EnableProgressBar;
            stopTestButton.Visible = true;
            #endregion

            #region Check the selected test and make the modifications according to it
            if (comboBox.SelectedIndex == 0) // 10 Clicks
            {
                currentTestName = "10-clicks";
                clickLabel.Text = 10 - clickCounter + " left";
            }
            else if (comboBox.SelectedIndex == 1) // 100 clicks
            {
                currentTestName = "100-clicks";
                clickLabel.Text = 100 - clickCounter + " left";
            }
            else if (comboBox.SelectedIndex == 2) // 1000 clicks
            {
                currentTestName = "1000-clicks";
                clickLabel.Text = 1000 - clickCounter + " left";
            }
            else if ((comboBox.SelectedIndex == 5) && (!typeCheckBox.Checked) && (!battleTypeCheckBox.Checked)) // Custom with clicks mode
            {
                int k = int.Parse(typeTextBox.Text);
                clickLabel.Text = k - clickCounter + " left";
            }
            else if ((comboBox.SelectedIndex == 5) && (battleTypeCheckBox.Checked))
            {
                if (!int.TryParse(battleHealthTextBox.Text, out battleModeHealth))
                {
                    EndTest();
                    MessageBox.Show("The entered value is not correct for Health", "Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!int.TryParse(battleDamageTextBox.Text, out battleModeDamage))
                {
                    EndTest();
                    MessageBox.Show("The entered value is not correct for Damage", "Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                clickLabel.Text = $"{battleModeHealth}hp remaining";
            }
            #endregion

            #region Disable menu while doing a test
            richTextBox1.Enabled = false;
            typeCheckBox.Enabled = false;
            typeTextBox.Enabled = false;
            battleTypeCheckBox.Enabled = false;
            battleHealthTextBox.Enabled = false;
            battleDamageTextBox.Enabled = false;
            applyConfigButton.Enabled = false;
            #endregion
        }

        private TimeSpan EndTest(bool fromStopButton = false)
        {
            #region Changing / removing / creating vars
            watcher.Stop();
            clickableButton.Text = "START";
            isTestRunning = false;
            timer.Enabled = false;
            timer.Stop();
            timer.Dispose();
            firstClickValuesCheck = false;
            maximumProgressBarChecked = false;
            double thisTestCps = Math.Round(ToClickPerSeconds(clickCounter, watcher.ElapsedMilliseconds), 3);
            stopTestButton.Visible = false;
            #endregion

            if (!fromStopButton)
            {
                #region Add this test cps to cpsAllTest (used below to write in the userStatsData)
                List<double> cpsAllTest;
                if (userStatsData.ClicksPerSecondsAllTest != null)
                {
                    cpsAllTest = new List<double>(userStatsData.ClicksPerSecondsAllTest);
                    cpsAllTest.Add(thisTestCps);
                }
                else
                {
                    cpsAllTest = new List<double>(new[] { thisTestCps });
                }
                #endregion

                #region Best score checking
                Dictionary<string, double> bestScores;
                if (userStatsData.BestScores != null)
                {
                    bestScores = userStatsData.BestScores;
                    double currentlyInArray;
                    if (bestScores.ContainsKey(currentTestName) && bestScores.TryGetValue(currentTestName, out currentlyInArray))
                    {
                        if (currentlyInArray < thisTestCps)
                        {
                            bestScores.Remove(currentTestName);
                            bestScores.Add(currentTestName, thisTestCps);
                        }
                    }
                    else
                    {
                        bestScores.Add(currentTestName, thisTestCps);
                    }
                }
                else
                {
                    bestScores = new Dictionary<string, double>
                    {
                        { currentTestName, thisTestCps }
                    };
                }
                #endregion

                #region Write new stats data
                StatsData newStatsData = new StatsData
                {
                    ClicksAverage = (userStatsData.TotalClicks + clickCounter) / (userStatsData.TotalTests + 1.0), // If 1, that's automatically round the double value
                    TotalClicks = userStatsData.TotalClicks + clickCounter,
                    TotalMsElapsedOnTest = userStatsData.TotalMsElapsedOnTest + watcher.ElapsedMilliseconds,
                    TotalTests = userStatsData.TotalTests + 1,
                    ClicksPerSecondsAverage = ToClickPerSeconds(userStatsData.TotalClicks + clickCounter, (long)userStatsData.TotalMsElapsedOnTest + watcher.ElapsedMilliseconds),
                    ClicksPerSecondsAllTest = cpsAllTest,
                    BestScores = bestScores
                };

                jData.WriteStatsData(newStatsData, "data\\stats.json");
                updateLocalStatsData();
                #endregion
            }

            #region Enable menus (which were disabled on test start)
            richTextBox1.Enabled = true;
            typeCheckBox.Enabled = true;
            typeTextBox.Enabled = true;
            battleTypeCheckBox.Enabled = true;
            battleHealthTextBox.Enabled = true;
            battleDamageTextBox.Enabled = true;
            applyConfigButton.Enabled = true;
            #endregion

            return watcher.Elapsed;
        }

        private void Timer(object source, ElapsedEventArgs e)
        {
            timeLabel.Invoke(new Action(() => timeLabel.Text = Math.Round(watcher.Elapsed.TotalSeconds, 1).ToString() + "s"));
            timeLabel.Invoke(new Action(() =>
            {
                timeLabel.Refresh();

                if (comboBox.SelectedIndex == 3)
                {
                    currentTestName = "10-secs";
                    if (watcher.ElapsedMilliseconds >= 10000)
                    {
                        long ms = watcher.ElapsedMilliseconds;
                        EndTest();
                        richTextBox1.Invoke(new Action(() =>
                        {
                            string s = $"Clicked {clickCounter} times in 10s\nClicks per second on average: \n{ToClickPerSeconds(clickCounter, ms)}cps\n";
                            richTextBox1.Text += s;
                            AddRichTextBoxSaveContent(s);
                        }));
                        MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else if (comboBox.SelectedIndex == 4)
                {
                    currentTestName = "1-sec";
                    if (watcher.ElapsedMilliseconds >= 1000)
                    {
                        long ms = watcher.ElapsedMilliseconds;
                        EndTest();
                        richTextBox1.Invoke(new Action(() =>
                        {
                            string s = $"Clicked {clickCounter} times in 1s\nClicks per second on average: \n{ToClickPerSeconds(clickCounter, ms)}cps\n";
                            richTextBox1.Text += s;
                            AddRichTextBoxSaveContent(s);
                        }));
                        MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else if ((comboBox.SelectedIndex == 5) && (typeCheckBox.Checked))
                {
                    int secFromTextBox = int.Parse(typeTextBox.Text);
                    if (watcher.ElapsedMilliseconds >= secFromTextBox * 1000)
                    {
                        long ms = watcher.ElapsedMilliseconds;
                        EndTest();
                        richTextBox1.Invoke(new Action(() =>
                        {
                            string s = $"Clicked {clickCounter} times in {secFromTextBox}s\nClicks per second on average: \n{ToClickPerSeconds(clickCounter, ms)}cps\n";
                            richTextBox1.Text += s;
                            AddRichTextBoxSaveContent(s);
                        }));
                        MessageBox.Show("Test finished", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }));
        }

        private static double ToClickPerSeconds(int totalClicks, long msElapsed)
        {
            return totalClicks / (msElapsed / 1000.0); // 1000.0 instead of 1000 fix the automatic rounding.
        }

        private void comboBox_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (comboBox.SelectedIndex == 5) // If selected item is "Custom"
            {
                CustomTestDisplay(true);
            }
            else
            {
                CustomTestDisplay(false);
            }
        }

        private void CustomTestDisplay(bool display)
        {
            if (display)
            {
                clickableButton.Size = new Size(380, 232);
                typeCheckBox.Enabled = true;
                typeCheckBox.Visible = true;
                battleTypeCheckBox.Enabled = true;
                battleTypeCheckBox.Visible = true;
                customConfigSaveButton.Visible = true;
                applyConfigButton.Visible = true;

                typeTextBox.Visible = true;
                typeTextBox.Enabled = true;
            }
            else
            {
                clickableButton.Size = new Size(634, 232);
                typeCheckBox.Enabled = false;
                typeCheckBox.Visible = false;
                battleTypeCheckBox.Enabled = false;
                battleTypeCheckBox.Visible = false;
                battleHealthTextBox.Enabled = false;
                battleHealthTextBox.Visible = false;
                battleDamageTextBox.Enabled = false;
                battleDamageTextBox.Visible = false;
                customConfigSaveButton.Visible = false;
                applyConfigButton.Visible = false;

                typeTextBox.Visible = false;
                typeTextBox.Enabled = false;
            }
        }

        private void typeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (typeCheckBox.Checked)
            {
                typeTextBox.PlaceholderText = "Seconds before end";
            }
            else
            {
                typeTextBox.PlaceholderText = "Max clicks";
            }
        }

        private void battleTypeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            battleHealthTextBox.Enabled = !battleHealthTextBox.Enabled;
            battleHealthTextBox.Visible = !battleHealthTextBox.Visible;
            battleDamageTextBox.Enabled = !battleDamageTextBox.Enabled;
            battleDamageTextBox.Visible = !battleDamageTextBox.Visible;

            if (battleTypeCheckBox.Checked)
            {
                typeCheckBox.Checked = false;
                typeCheckBox.Enabled = false;
                typeTextBox.Enabled = false;
                typeTextBox.Visible = false;
            }
            else
            {
                typeCheckBox.Enabled = true;
                typeTextBox.Enabled = true;
                typeTextBox.Visible = true;
            }
        }

        private void SoundEffectCheckedChanged(object sender, EventArgs e)
        {
            Data dateToWrite = new Data
            {
                EnableSound = settingsForm.soundEffectCheckBox.Checked,
                EnableProgressBar = userData.EnableProgressBar,
                DefaultTest = userData.DefaultTest,
                CustomConfig = userData.CustomConfig,
                RichTextBoxSaveEnabled = userData.RichTextBoxSaveEnabled,
                RichTextBoxContent = userData.RichTextBoxContent,
                SaveBestScoreForEachTest = userData.SaveBestScoreForEachTest
            };

            jData.WriteData(dateToWrite, "data\\data.json");

            updateLocalData();
        }

        private void SaveLogsCheckedChanged(object sender, EventArgs e)
        {
            Data dateToWrite = new Data
            {
                EnableSound = userData.EnableSound,
                EnableProgressBar = userData.EnableProgressBar,
                DefaultTest = userData.DefaultTest,
                CustomConfig = userData.CustomConfig,
                RichTextBoxSaveEnabled = settingsForm.saveLogsCheckBox.Checked,
                RichTextBoxContent = userData.RichTextBoxContent,
                SaveBestScoreForEachTest = userData.SaveBestScoreForEachTest
            };

            jData.WriteData(dateToWrite, "data\\data.json");

            updateLocalData();
        }

        private void saveButton_Click(object sender, EventArgs e) 
        {
            Data dateToWrite = new Data
            {
                EnableSound = userData.EnableSound,
                EnableProgressBar = userData.EnableProgressBar,
                DefaultTest = comboBox.SelectedIndex,
                CustomConfig = userData.CustomConfig,
                RichTextBoxSaveEnabled = userData.RichTextBoxSaveEnabled,
                RichTextBoxContent = userData.RichTextBoxContent,
                SaveBestScoreForEachTest = userData.SaveBestScoreForEachTest
            };

            if (!jData.WriteData(dateToWrite, "data\\data.json"))
            {
                MessageBox.Show("An error occured: Can't write json data.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            updateLocalData();
        }

        private void customConfigSaveButton_Click(object sender, EventArgs e)
        {
            string[] config = {
                $"{((!typeCheckBox.Checked && !battleTypeCheckBox.Checked) ? "click" : "timer")}",
                $"{(battleTypeCheckBox.Checked ? "battle" : "none")}",
                $"{typeTextBox.Text}",
                $"{battleHealthTextBox.Text}",
                $"{battleDamageTextBox.Text}"
            };

            Data dateToWrite = new Data
            {
                EnableSound = userData.EnableSound,
                EnableProgressBar = userData.EnableProgressBar,
                DefaultTest = userData.DefaultTest,
                CustomConfig = config,
                RichTextBoxSaveEnabled = userData.RichTextBoxSaveEnabled,
                RichTextBoxContent = userData.RichTextBoxContent,
                SaveBestScoreForEachTest = userData.SaveBestScoreForEachTest
            };

            if (!jData.WriteData(dateToWrite, "data\\data.json"))
            {
                MessageBox.Show("An error occured: Can't write json data.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            updateLocalData();
        }

        private void updateLocalData()
        {
            userData = jData.ReadData("data\\data.json");
            if (userData == null)
                MessageBox.Show("An error occured: Json file is not working.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void updateLocalStatsData()
        {
            userStatsData = jData.ReadStatsData("data\\stats.json");
            if (userStatsData == null)
                MessageBox.Show("An error occured: Json file (stats) is not working.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void applyConfigButton_Click(object sender, EventArgs e)
        {
            if (userData.CustomConfig != null)
            {
                if (userData.CustomConfig[1] == "battle")
                    battleTypeCheckBox.CheckState = CheckState.Checked;
                else
                    typeCheckBox.CheckState = CheckState.Unchecked;

                if (userData.CustomConfig[0] == "click")
                    typeCheckBox.CheckState = CheckState.Unchecked;
                else if (!battleTypeCheckBox.Checked)
                    typeCheckBox.CheckState = CheckState.Checked;

                typeTextBox.Text = userData.CustomConfig[2];
                battleHealthTextBox.Text = userData.CustomConfig[3];
                battleDamageTextBox.Text = userData.CustomConfig[4];
            }
            else
            {
                MessageBox.Show("Operation cancelled: Can't apply a null configuration.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void settingsIcon_Click(object sender, EventArgs e)
        {
            if (IsClientNotDoingTest())
            {
                if (!settingsForm.Visible)
                {
                    settingsFormUtilities.InitializeSettingsFormParameters(settingsForm, userData.EnableSound, userData.RichTextBoxSaveEnabled, userData.EnableProgressBar, userData.SaveBestScoreForEachTest);
                    settingsForm.ShowDialog();
                }
                else
                    settingsForm.Hide();
            }
        }

        private void statsButton_Click(object sender, EventArgs e)
        {
            if (IsClientNotDoingTest())
            {
                if (!statsForm.Visible)
                {
                    if (statsForm.chartPanel.BackgroundImage != null)
                        statsForm.chartPanel.BackgroundImage.Dispose();

                    Cursor.Current = Cursors.WaitCursor;
                    statsForm.totalClicksLabel.Text = $"Total Clicks: {userStatsData.TotalClicks}";
                    statsForm.totalTestsLabel.Text = $"Total Tests: {userStatsData.TotalTests}";
                    statsForm.clicksAverageLabel.Text = $"Clicks per test average: {Math.Round(userStatsData.ClicksAverage, 3)}";
                    statsForm.timeElapsedLabel.Text = $"Time elapsed on tests: {Math.Round(userStatsData.TotalMsElapsedOnTest / 1000, 2)}s";
                    statsForm.clicksPerSecondsAverageLabel.Text = $"Clicks per seconds average: {Math.Round(userStatsData.ClicksPerSecondsAverage, 3)}/s";
                    StatsUtilities statsUtilities = new StatsUtilities();
                    statsUtilities.RemoveIncorrectsValuesFromStats(userStatsData);
                    if (statsUtilities.GetChart())
                        statsForm.chartPanel.BackgroundImage = Image.FromFile("data\\chart.png");
                    else
                        MessageBox.Show("An error occured to generate the chart: can't display cps chart.\nContact support or create issue on github", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    statsForm.ShowDialog();
                    Cursor.Current = Cursors.Default;
                }
                else
                    statsForm.Hide();
            }
        }

        private void AddRichTextBoxSaveContent(string content)
        {
            Data dateToWrite = new Data
            {
                EnableSound = userData.EnableSound,
                EnableProgressBar = userData.EnableProgressBar,
                DefaultTest = userData.DefaultTest,
                CustomConfig = userData.CustomConfig,
                RichTextBoxSaveEnabled = userData.RichTextBoxSaveEnabled,
                RichTextBoxContent = userData.RichTextBoxContent + content,
                SaveBestScoreForEachTest = userData.SaveBestScoreForEachTest
            };

            if (!jData.WriteData(dateToWrite, "data\\data.json"))
            {
                MessageBox.Show("An error occured: Can't write json data.\nContact support or create issue on github", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            updateLocalData();
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }

        private void ResetDataFolder(object sender, EventArgs e)
        {
            DialogResult resultMsgBox = MessageBox.Show("Are you really sure?", "CClick", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (resultMsgBox == DialogResult.Yes)
            {
                Directory.Delete("data", true);
                MessageBox.Show("CClick need to restart.", "CClick", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
        }

        private void ResetStatistics(object sender, EventArgs e)
        {
            DialogResult resultMsgBox = MessageBox.Show("Are you really sure?", "CClick", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (resultMsgBox == DialogResult.Yes)
            {
                StatsData statsReset = new StatsData
                {
                    ClicksAverage = 0.0,
                    TotalClicks = 0,
                    TotalMsElapsedOnTest = 0.0,
                    TotalTests = 0,
                    ClicksPerSecondsAverage = 0.0,
                    ClicksPerSecondsAllTest = null,
                    BestScores = null
                };
                jData.WriteStatsData(statsReset, "data\\stats.json");
                updateLocalStatsData();
            }
        }

        private void ProgressBarCheckedChanged(object sender, EventArgs e)
        {
            Data dateToWrite = new Data
            {
                EnableSound = userData.EnableSound,
                EnableProgressBar = settingsForm.progressBarCheckbox.Checked,
                DefaultTest = userData.DefaultTest,
                CustomConfig = userData.CustomConfig,
                RichTextBoxSaveEnabled = userData.RichTextBoxSaveEnabled,
                RichTextBoxContent = userData.RichTextBoxContent,
                SaveBestScoreForEachTest = userData.SaveBestScoreForEachTest
            };

            jData.WriteData(dateToWrite, "data\\data.json");

            updateLocalData();
        }

        private void battleHealthTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(battleHealthTextBox.Text, out _) && battleHealthTextBox.Text != "")
            {
                MessageBox.Show("Health value must be an integer.");
                battleHealthTextBox.Text = "";
            }
        }

        private void battleDamageTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(battleDamageTextBox.Text, out _) && battleHealthTextBox.Text != "")
            {
                MessageBox.Show("Damage value must be an integer.");
                battleDamageTextBox.Text = "";
            }
        }

        private void typeTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(typeTextBox.Text, out _) && battleHealthTextBox.Text != "")
            {
                MessageBox.Show($"{(typeCheckBox.Checked ? "Seconds value" : "Clicks limit value")} must be an integer.");
                typeTextBox.Text = "";
            }
        }

        private void SaveBestScoreCheckedChanged(object sender, EventArgs e)
        {
            Data dateToWrite = new Data
            {
                EnableSound = userData.EnableSound,
                EnableProgressBar = userData.EnableProgressBar,
                DefaultTest = userData.DefaultTest,
                CustomConfig = userData.CustomConfig,
                RichTextBoxSaveEnabled = userData.RichTextBoxSaveEnabled,
                RichTextBoxContent = userData.RichTextBoxContent,
                SaveBestScoreForEachTest = settingsForm.saveBestCheckBox.Checked
            };

            jData.WriteData(dateToWrite, "data\\data.json");

            updateLocalData();
        }

        private void bestScoresMenuButton_Click(object sender, EventArgs e)
        {
            if (IsClientNotDoingTest())
            {
                if (!bestScoresForm.Visible)
                {
                    if (userStatsData.BestScores != null)
                    {
                        if (userStatsData.BestScores.TryGetValue("10-clicks", out double cps10clicks))
                            bestScoresForm.clicks10.Text = $"10 clicks: {cps10clicks} click/s";

                        if (userStatsData.BestScores.TryGetValue("100-clicks", out double cps100clicks))
                            bestScoresForm.clicks100.Text = $"100 clicks: {cps100clicks} click/s";

                        if (userStatsData.BestScores.TryGetValue("1000-clicks", out double cps1000clicks))
                            bestScoresForm.clicks1000.Text = $"1000 clicks: {cps1000clicks} click/s";

                        if (userStatsData.BestScores.TryGetValue("1-sec", out double cps1second))
                            bestScoresForm.sec1.Text = $"1 second: {cps1second} click/s";

                        if (userStatsData.BestScores.TryGetValue("10-secs", out double cps10seconds))
                            bestScoresForm.sec10.Text = $"10 seconds: {cps10seconds} click/s";
                    }

                    bestScoresForm.ShowDialog();
                }
                else
                    bestScoresForm.Hide();
            }
        }

        private bool IsClientNotDoingTest()
        {
            if (isTestRunning)
                System.Media.SystemSounds.Exclamation.Play();
            else
                return true;
            return false;
        }

        private void stopTestButton_Click(object sender, EventArgs e)
        {
            EndTest(true);
            System.Media.SystemSounds.Asterisk.Play();
        }
    }
}
