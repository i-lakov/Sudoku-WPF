using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using Microsoft.Win32;
using System.IO;

namespace CourseProject
{
    /// <summary>
    /// Interaction logic for Sudoku.xaml
    /// </summary>
    public partial class Sudoku : UserControl
    {
        #region Data members
        private int solvedCount = 0, totalCount = 0;        // statistic purposes
        private bool isInitialized = false;                 // toggles the OnTextChanged event
        private int s, m;                                   // seconds & minutes elapsed
        private int[,] matrix = new int[9, 9];              // holds the solution
        private int[,] visibleMatrix = new int[9, 9];       // holds the user progress
        private int[,] immutableMatrix = new int[9, 9];     // holds the originally given values
        private DispatcherTimer timer;

        Stack<Tuple<int, int, int>> undoStack;
        Stack<Tuple<int, int, int>> redoStack;
        #endregion

        public Sudoku()
        {
            InitializeComponent();

            // Timer initialization.
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timeTick;

            // Initialization of undo & redo stacks.
            undoStack = new Stack<Tuple<int, int, int>>();
            redoStack = new Stack<Tuple<int, int, int>>();

            // Can't save an empty game, can you?!
            MenuSave.IsEnabled = false;
        }

        #region Timer methods
        /// <summary>
        /// Timer utility function.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timeTick(object sender, EventArgs e)
        {
            if (s == 59)
            {
                s = 0;
                m++;
            }
            else s++;
            LblTimer.Content = $"{m:D2}:{s:D2}";
        }

        /// <summary>
        /// Starts the timer by given seconds and minutes to start from, by default - 0.
        /// </summary>
        /// <param name="sec"></param>
        /// <param name="min"></param>
        private void StartTimer(int sec = 0, int min = 0)
        {
            s = sec; m = min;
            timer.Start();
        }
        #endregion

        #region Sudoku matrix generation methods
        /// <summary>
        /// Generates a new sudoku game randomly.
        /// </summary>
        /// <param name="difficulty"></param>
        private void GenerateTable(Difficulty difficulty)
        {
            Random rand = new Random();
            int current = 0;
            SortedSet<int> alreadyExisting = new SortedSet<int>();
            List<int> leadLine = new List<int>();

            // Initialize all matrices to 0
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    matrix[i, j] = 0;
                    visibleMatrix[i, j] = 0;
                    immutableMatrix[i, j] = 0;
                }
            }

            // Generate a "lead line", which is then used to fill the whole board.
            for (int i = 0; i < 9; i++)
            {
                current = rand.Next(1, 10);
                if (!alreadyExisting.Contains(current))
                {
                    alreadyExisting.Add(current);
                    matrix[0, i] = current;
                    leadLine.Add(current);
                }
                else i--;
            }

            // Move the lead line left by 3 digits twice.
            for (int i = 1; i < 3; i++)
            {
                leadLine.Add(leadLine[0]);
                leadLine.Add(leadLine[1]);
                leadLine.Add(leadLine[2]);
                leadLine.RemoveRange(0, 3);
                for (int j = 0; j < 9; j++)
                {
                    matrix[i, j] = leadLine[j];
                }
            }

            // Move the lead line left by 1 digit and then two more times by 3 digits and repeat.
            for (int multiplier = 1; multiplier < 3; multiplier++)
            {
                leadLine.Add(leadLine[0]);
                leadLine.RemoveAt(0);
                for (int j = 0; j < 9; j++)
                {
                    matrix[3 * multiplier, j] = leadLine[j];
                }

                for (int i = 1; i < 3; i++)
                {
                    leadLine.Add(leadLine[0]);
                    leadLine.Add(leadLine[1]);
                    leadLine.Add(leadLine[2]);
                    leadLine.RemoveRange(0, 3);
                    for (int j = 0; j < 9; j++)
                    {
                        matrix[i + (3 * multiplier), j] = leadLine[j];
                    }
                }
            }

            // Shuffle lines so that any number patterns are broken.
            int randomSection, randomLineFrom, randomLineTo;
            for(int times = 0; times < 5; times++)
            {
                randomSection = rand.Next(0, 3);
                randomLineFrom = rand.Next(0, 3);
                randomLineTo = rand.Next(0, 3);
                List<int> movingBuffer = new List<int>();
                for (int i = 0; i < 9; i++)
                {
                    movingBuffer.Add(matrix[i, randomSection * 3 + randomLineFrom]);
                }
                for (int i = 0; i < 9; i++)
                {
                    matrix[i, randomSection * 3 + randomLineFrom] = matrix[i, randomSection * 3 + randomLineTo];
                }
                for (int i = 0; i < 9; i++)
                {
                    matrix[i, randomSection * 3 + randomLineTo] = movingBuffer[i];
                }
            }

            // Starts the timer of the new game (from 00:00).
            StartTimer();
            ShowInitialTable(difficulty);
        }

        /// <summary>
        /// Displays the starting numbers, based on set difficulty.
        /// </summary>
        /// <param name="difficulty"></param>
        private void ShowInitialTable(Difficulty difficulty)
        {
            // The amount of cells to show when the game starts.
            int startingCells = 0;
            switch (difficulty)
            {
                case Difficulty.EASY:
                    startingCells = 32;
                    break;
                case Difficulty.MEDIUM:
                    startingCells = 28;
                    break;
                case Difficulty.HARD:
                    startingCells = 24;
                    break;
                default:
                    startingCells = 28;
                    break;
            }

            Random rand = new Random();
            int row = 0;
            int col = 0;

            // List that holds all the shown cells up to now,
            // to prevent the same cell from being shown twice,
            // thus reducing the number of total shown cells.
            var tuples = new List<Tuple<int, int>>();
            Tuple<int, int> tempTuple;
            for (int i = 0; i < startingCells; i++)
            {
                // Pick a random cell to show.
                row = rand.Next(0, 9);
                col = rand.Next(0, 9);
                tempTuple = new Tuple<int, int>(row, col);

                // If it's already been shown, reduce counter by one.
                if (tuples.Contains(tempTuple))
                {
                    i--;
                }
                // If it's not been shown yet, show it and add it to the list.
                else
                {
                    tuples.Add(tempTuple);
                    visibleMatrix[row, col] = matrix[row, col];
                    immutableMatrix[row, col] = 1;
                }
            }
            ShowVisibleTable();
            InitializationComplete();
        }
        #endregion

        /// <summary>
        /// Utility function called when table initialization is completed.
        /// </summary>
        private void InitializationComplete()
        {
            // Enables the solve button and menu option.
            BtnSolve.IsEnabled = true;
            MenuSolve.IsEnabled = true;

            // Enables invokation of the OnTextChanged event.
            isInitialized = true;

            // Makes the sudoku board editable, except for the originally given boxes.
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    TextBox textbox = GridContainer.FindName("TxtX" + i + "Y" + j) as TextBox;
                    if (textbox != null)
                    {
                        textbox.IsReadOnly = false;
                        textbox.IsEnabled = true;
                    }
                    if (immutableMatrix[j, i] == 1)
                    {
                        textbox.IsReadOnly = true;
                    }
                }
            }

            // Reset undo & redo stacks, because we're starting a new game.
            undoStack.Clear();
            redoStack.Clear();
            BtnRedo.IsEnabled = false;
            MenuRedo.IsEnabled = false;
            BtnUndo.IsEnabled = false;
            MenuUndo.IsEnabled = false;
            MenuSave.IsEnabled = true;
        }

        /// <summary>
        /// Displays the numbers, filled in up to now.
        /// </summary>
        private void ShowVisibleTable()
        {
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    string asString = visibleMatrix[j, i].ToString();
                    UpdateTextBox(i, j, asString);
                }
            }
        }

        /// <summary>
        /// Solves the sudoku.
        /// </summary>
        private void SolveTable()
        {
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    visibleMatrix[j, i] = matrix[j, i];
                    string asString = visibleMatrix[j, i].ToString();
                    UpdateTextBox(i, j, asString);
                }
            }
        }


        #region Updating textboxes methods
        /// <summary>
        /// Find and updates the textbox in the sudoku by given coordinates and what to write (string).
        /// </summary>
        /// <param name="row"></param>
        /// <param name="column"></param>
        /// <param name="text"></param>
        private void UpdateTextBox(int row, int column, string text)
        {
            TextBox textBox = GridContainer.FindName("TxtX" + row + "Y" + column) as TextBox;
            if (textBox != null)
            {
                if (text == "0")
                {
                    textBox.Text = "";
                }
                else textBox.Text = text;
            }
        }

        /// <summary>
        /// Event used to update matrix when the used changes a value on the gameboard.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Checking if it's user input and not input during initialization of the game.
            if (isInitialized == true && BtnSolve.IsEnabled == true)
            {
                // Assign sender textbox to a field and get its coordinates.
                TextBox textbox = ((TextBox)sender);
                int col = (int)textbox.GetValue(Grid.ColumnProperty);
                int row = (int)textbox.GetValue(Grid.RowProperty);
                row = row - 2;

                if (textbox.Text != "")
                {
                    // Checking that value is valid (1-9) before proceeding.
                    bool conversionPassed;
                    int valueToCheck;
                    conversionPassed = int.TryParse(textbox.Text, out valueToCheck);

                    if (conversionPassed && valueToCheck >= 1 && valueToCheck <= 9 && textbox.Text.Length == 1)
                    {
                        // Toggling visibility of button & menu option for Undo.
                        BtnUndo.IsEnabled = true;
                        MenuUndo.IsEnabled = true;

                        var oldValue = visibleMatrix[row, col];
                        visibleMatrix[row, col] = int.Parse(textbox.Text);
                        var tempTuple = new Tuple<int, int, int>(row, col, oldValue);
                        undoStack.Push(tempTuple);
                    }
                    else
                    {
                        isInitialized = false;
                        textbox.Text = visibleMatrix[row, col] == 0 ? "" : visibleMatrix[row, col].ToString();
                        isInitialized = true;
                    }
                }
                else
                {
                    // Toggling visibility of button & menu option for Undo.
                    BtnUndo.IsEnabled = true;
                    MenuUndo.IsEnabled = true;

                    var oldValue = visibleMatrix[row, col];
                    visibleMatrix[row, col] = 0;
                    var tempTuple = new Tuple<int, int, int>(row, col, oldValue);
                    undoStack.Push(tempTuple);
                }
            } // end isInitialized check

            if (IsFilledUp())
            {
                if (IsSolved())
                {
                    if (BtnSolve.IsEnabled)
                    {
                        MessageBox.Show(Application.Current.MainWindow, "Congratulations! You've solved the Sudoku!",
                            "Game won", MessageBoxButton.OK);
                    }

                    // Add +1 to the statistics counter for number of games solved in this session.
                    solvedCount++;

                    // Turning buttons off, as the played is expected to start/load a new game.
                    BtnUndo.IsEnabled = false;
                    BtnRedo.IsEnabled = false;
                    BtnSolve.IsEnabled = false;
                    MenuSolve.IsEnabled = false;
                    MenuUndo.IsEnabled = false;
                    MenuRedo.IsEnabled = false;
                    MenuSave.IsEnabled = false;

                    // Making the whole board uneditable.
                    for (int i = 0; i < 9; i++)
                    {
                        for (int j = 0; j < 9; j++)
                        {
                            TextBox textbox = GridContainer.FindName("TxtX" + i + "Y" + j) as TextBox;
                            textbox.IsEnabled = false;
                        }
                    }

                    // Stopping the timer.
                    timer.Stop();
                }
                else
                {
                    if (BtnSolve.IsEnabled == true)
                    {
                        MessageBox.Show(Application.Current.MainWindow, "There's a mistake, game not finished.",
                        "Game not finished", MessageBoxButton.OK);
                    }
                }
            }
        }
        #endregion

        #region Boolean test functions
        /// <summary>
        /// Checks if sudoku solution is all filled up.
        /// </summary>
        /// <returns>true if correct, false if incorrect</returns>
        private bool IsFilledUp()
        {
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (visibleMatrix[i, j] == 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if sudoku solution is correct.
        /// </summary>
        /// <returns>true if correct, false if incorrect</returns>
        private bool IsSolved()
        {
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (matrix[i, j] != visibleMatrix[i, j])
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion

        #region Button & Menu events
        private void BtnSolve_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show("Are you sure you want to see the solution? This cannot be undone!", "See solution?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                BtnSolve.IsEnabled = false;
                MenuSolve.IsEnabled = false;
                SolveTable();
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want exit? Any unsaved progress will be lost!", "Are you sure?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Environment.Exit(0);
            }
        }

        private void MenuSave_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog();
            saveDialog.AddExtension = true;
            saveDialog.Filter = "txt files (*.txt)|*.txt";
            saveDialog.Title = "Save a Sudoku";
            saveDialog.InitialDirectory = @"assets\saves";
            saveDialog.OverwritePrompt = true;

            if (saveDialog.ShowDialog() == true)
            {
                if (saveDialog.FileName != "")
                {
                    FileStream stream = (FileStream)saveDialog.OpenFile();
                    string solutionLine = string.Empty;
                    string currentStateLine = string.Empty;
                    string immutableLine = string.Empty;

                    for (int i = 0; i < 9; i++)
                    {
                        for (int j = 0; j < 9; j++)
                        {
                            solutionLine += matrix[i, j];
                            currentStateLine += visibleMatrix[i, j];
                            immutableLine += immutableMatrix[i, j];
                        }
                    }

                    string level = (string)LblDifficulty.Content;
                    string timeElapsed = (string)LblTimer.Content;

                    // Store the solution of the current sudoku.
                    stream.Write(Encoding.ASCII.GetBytes(solutionLine), 0, 81);
                    stream.Write(Encoding.ASCII.GetBytes("\n"), 0, 1);
                    // Store user's progress up to now.
                    stream.Write(Encoding.ASCII.GetBytes(currentStateLine), 0, 81);
                    stream.Write(Encoding.ASCII.GetBytes("\n"), 0, 1);
                    // Store the originally given boxes
                    stream.Write(Encoding.ASCII.GetBytes(immutableLine), 0, 81);
                    stream.Write(Encoding.ASCII.GetBytes("\n"), 0, 1);
                    // Store the difficulty level.
                    stream.Write(Encoding.ASCII.GetBytes(level), 0, 1);
                    stream.Write(Encoding.ASCII.GetBytes("\n"), 0, 1);
                    // Store minutes elapsed.
                    stream.Write(Encoding.ASCII.GetBytes(timeElapsed), 0, 2);
                    stream.Write(Encoding.ASCII.GetBytes("\n"), 0, 1);
                    // Store seconds elapsed.
                    stream.Write(Encoding.ASCII.GetBytes(timeElapsed), 3, 2);
                    stream.Close();
                }
            }
        }

        private void MenuLoad_Click(object sender, RoutedEventArgs e)
        {
            string path = string.Empty;
            var fileDialog = new OpenFileDialog();
            isInitialized = false;

            fileDialog.InitialDirectory = @"assets\saves";
            fileDialog.Title = "Load a Sudoku";
            fileDialog.Filter = "txt files (*.txt)|*.txt";
            if (fileDialog.ShowDialog() == true)
            {
                path = fileDialog.FileName;
            }

            try
            {
                // Read the game data from the save file.
                var reader = new StreamReader(path);
                string solutionMatrixLine = reader.ReadLine();
                string currentStateMatrixLine = reader.ReadLine();
                string immutableMatrixLine = reader.ReadLine();
                string level = reader.ReadLine();
                int minutes = int.Parse(reader.ReadLine());
                int seconds = int.Parse(reader.ReadLine());

                // Assign read values to the current game i.e. "load" the saved game.
                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        matrix[i, j] = Convert.ToInt32(solutionMatrixLine[j + i * 9]) - '0';
                        visibleMatrix[i, j] = Convert.ToInt32(currentStateMatrixLine[j + i * 9]) - '0';
                        immutableMatrix[i, j] = Convert.ToInt32(immutableMatrixLine[j + i * 9]) - '0';
                    }
                }

                // Starts the timer, starting at the values when the user saved their game.
                StartTimer(seconds, minutes);
                ShowVisibleTable();

                switch (level)
                {
                    case "E":
                        LblDifficulty.Content = "Easy Mode";
                        break;
                    case "M":
                        LblDifficulty.Content = "Medium Mode";
                        break;
                    case "H":
                        LblDifficulty.Content = "Hard Mode";
                        break;
                    default:
                        LblDifficulty.Content = "Medium Mode";
                        break;
                }

                // Add +1 to the statistics counter for number of games played in this session.
                totalCount++;

                InitializationComplete();
            }
            catch (Exception exc)
            {
                Console.WriteLine("Exception: " + exc.Message);
            }
        }

        private void MenuNewGame_Click(object sender, RoutedEventArgs e)
        {
            Difficulty difficulty;

            // Assigns the difficulty chosen by the user to enum type "difficulty".
            switch (((MenuItem)sender).Header)
            {
                case "_Easy":
                    difficulty = Difficulty.EASY;
                    LblDifficulty.Content = "Easy Mode";
                    break;
                case "_Medium":
                    difficulty = Difficulty.MEDIUM;
                    LblDifficulty.Content = "Medium Mode";
                    break;
                case "_Hard":
                    difficulty = Difficulty.HARD;
                    LblDifficulty.Content = "Hard Mode";
                    break;
                default:
                    difficulty = Difficulty.MEDIUM;
                    LblDifficulty.Content = "Medium Mode";
                    break;
            }

            // Setting isInitialized to false so that the Event OnTextChanged is bypassed,
            // as there is no need to change matrices twice.
            isInitialized = false;

            // Add +1 to the statistics counter for number of games played in this session.
            totalCount++;

            GenerateTable(difficulty);
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            // Setting isInitialized to false so that the Event OnTextChanged is bypassed,
            // as there is no need to change matrices twice.
            isInitialized = false;

            // Save the current value to the redo stack.
            var tuple = undoStack.Peek();
            var redoTuple = new Tuple<int, int, int>(tuple.Item1, tuple.Item2, visibleMatrix[tuple.Item1, tuple.Item2]);
            redoStack.Push(redoTuple);
            undoStack.Pop();

            // Toggling visibility of buttons & menu options for Redo & Undo
            BtnRedo.IsEnabled = true;
            MenuRedo.IsEnabled = true;
            if (undoStack.Count == 0)
            {
                BtnUndo.IsEnabled = false;
                MenuUndo.IsEnabled = false;
            }

            // Updating matrix & gameboard with the undoed value.
            visibleMatrix[tuple.Item1, tuple.Item2] = tuple.Item3;
            UpdateTextBox(tuple.Item2, tuple.Item1, tuple.Item3.ToString());

            // Setting isInitialized back to true, to enable event OnTextChanged invokation.
            isInitialized = true;
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            // Setting isInitialized to false so that the Event OnTextChanged is bypassed,
            // as there is no need to change matrices twice.
            isInitialized = false;

            // Save the current value to the undo stack.
            var tuple = redoStack.Peek();
            var undoTuple = new Tuple<int, int, int>(tuple.Item1, tuple.Item2, visibleMatrix[tuple.Item1, tuple.Item2]);
            undoStack.Push(undoTuple);
            redoStack.Pop();

            // Toggling visibility of buttons & menu options for Redo & Undo
            BtnUndo.IsEnabled = true;
            MenuUndo.IsEnabled = true;
            if (redoStack.Count == 0)
            {
                BtnRedo.IsEnabled = false;
                MenuRedo.IsEnabled = false;
            }

            // Updating matrix & gameboard with the redoed value.
            visibleMatrix[tuple.Item1, tuple.Item2] = tuple.Item3;
            UpdateTextBox(tuple.Item2, tuple.Item1, tuple.Item3.ToString());

            // Setting isInitialized back to true, to enable event OnTextChanged invokation.
            isInitialized = true;
        }

        private void MenuStatistics_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Application.Current.MainWindow, $"During this session, you\n\nSolved  {solvedCount}  sudokus\nPlayed  {totalCount}  games in total",
                "Session statistics", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion
    }
}
