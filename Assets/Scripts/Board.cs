using UnityEngine;

[DefaultExecutionOrder(-1)]
public class Board : MonoBehaviour
{
    // --- НОВОЕ ---------------------------------------------------------------
#if UNITY_IOS || UNITY_ANDROID
    private TouchScreenKeyboard keyboard;
    private string previousKeyboardText = "";

     private void CloseKeyboard()
    {
        TouchScreenKeyboard.hideInput = true;
        keyboard.active = false;
        keyboard = null;
        previousKeyboardText = "";
    }
#endif
    // ------------------------------------------------------------------------

    private static readonly KeyCode[] SUPPORTED_KEYS = {
        KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F,
        KeyCode.G, KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L,
        KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P, KeyCode.Q, KeyCode.R,
        KeyCode.S, KeyCode.T, KeyCode.U, KeyCode.V, KeyCode.W, KeyCode.X,
        KeyCode.Y, KeyCode.Z,
    };

    private static readonly string[] SEPARATOR = { "\r\n", "\r", "\n" };

    private Row[] rows;
    private int rowIndex;
    private int columnIndex;

    private string[] solutions;
    private string[] validWords;
    private string word;

    [Header("Tiles")]
    public Tile.State emptyState;
    public Tile.State occupiedState;
    public Tile.State correctState;
    public Tile.State wrongSpotState;
    public Tile.State incorrectState;

    [Header("UI")]
    public GameObject tryAgainButton;
    public GameObject newWordButton;
    public GameObject invalidWordText;

    // ──────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle
    private void Awake()               => rows = GetComponentsInChildren<Row>();

    private void Start()               { LoadData(); NewGame(); }

    private void OnEnable()            { tryAgainButton.SetActive(false); newWordButton.SetActive(false); }

    private void OnDisable()           { tryAgainButton.SetActive(true);  newWordButton.SetActive(true);  }
    #endregion
    // ──────────────────────────────────────────────────────────────────────────
    #region Public API
    public void NewGame()              { ClearBoard(); SetRandomWord();   enabled = true; }

    public void TryAgain()             { ClearBoard();                    enabled = true; }
    #endregion
    // ──────────────────────────────────────────────────────────────────────────
    private void LoadData()
    {
        TextAsset t = Resources.Load<TextAsset>("official_wordle_common");
        solutions   = t.text.Split(SEPARATOR, System.StringSplitOptions.None);

        t           = Resources.Load<TextAsset>("official_wordle_all");
        validWords  = t.text.Split(SEPARATOR, System.StringSplitOptions.None);
    }

    private void SetRandomWord()
    {
        word = solutions[Random.Range(0, solutions.Length)].ToLower().Trim();
    }

    // ──────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        Row currentRow = rows[rowIndex];

#if UNITY_IOS || UNITY_ANDROID
        HandleTouchScreenKeyboard(currentRow);
#else
        HandleEditorKeyboard(currentRow);
#endif
    }
    // ──────────────────────────────────────────────────────────────────────────
    #region Input Handlers
#if UNITY_IOS || UNITY_ANDROID
    // ——— Мобильная системная клавиатура ———
    private void HandleTouchScreenKeyboard(Row currentRow)
    {
        // Открываем клавиатуру, если она ещё не отображается
        if (keyboard == null || !TouchScreenKeyboard.visible)
        {
            keyboard           = TouchScreenKeyboard.Open(
                "", TouchScreenKeyboardType.Default, false, false, false, false, "Enter word");
            previousKeyboardText = "";
            return;
        }

        // Считываем текущее содержимое поля ввода
        string text = keyboard.text.ToLower();

        // BACKSPACE: пользователь стер символ
        if (text.Length < previousKeyboardText.Length && columnIndex > 0)
        {
            columnIndex--;
            currentRow.tiles[columnIndex].SetLetter('\0');
            currentRow.tiles[columnIndex].SetState(emptyState);
            invalidWordText.SetActive(false);
        }
        // ДОПИСАЛИ СИМВОЛ
        else if (text.Length > previousKeyboardText.Length)
        {
            char ch = text[^1];

            if (ch >= 'a' && ch <= 'z')
            {
                currentRow.tiles[columnIndex].SetLetter(ch);
                currentRow.tiles[columnIndex].SetState(occupiedState);
                columnIndex++;
            }
        }

        previousKeyboardText = text;

        // Автосабмит, когда строка заполнена
        if (columnIndex >= currentRow.tiles.Length)
        {
            SubmitRow(currentRow);
        }
    }
#endif

    // ——— Старый путь для редактора/ПК ———
    private void HandleEditorKeyboard(Row currentRow)
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            columnIndex = Mathf.Max(columnIndex - 1, 0);
            currentRow.tiles[columnIndex].SetLetter('\0');
            currentRow.tiles[columnIndex].SetState(emptyState);
            invalidWordText.SetActive(false);
        }
        else if (columnIndex >= currentRow.tiles.Length)
        {
            // В редакторе по‑старому жмём Enter
            if (Input.GetKeyDown(KeyCode.Return))
                SubmitRow(currentRow);
        }
        else
        {
            for (int i = 0; i < SUPPORTED_KEYS.Length; i++)
            {
                if (Input.GetKeyDown(SUPPORTED_KEYS[i]))
                {
                    currentRow.tiles[columnIndex].SetLetter((char)SUPPORTED_KEYS[i]);
                    currentRow.tiles[columnIndex].SetState(occupiedState);
                    columnIndex++;
                    break;
                }
            }

            if (columnIndex >= currentRow.tiles.Length)
                SubmitRow(currentRow);
        }
    }
    #endregion
    // ──────────────────────────────────────────────────────────────────────────
    #region Game Logic
    private void SubmitRow(Row row)
    {
        string remaining = word;

        // 1. Правильные буквы
        for (int i = 0; i < row.tiles.Length; i++)
        {
            Tile tile = row.tiles[i];

            if (tile.letter == word[i])
            {
                tile.SetState(correctState);
                remaining = remaining.Remove(i, 1).Insert(i, " ");
            }
            else if (!word.Contains(tile.letter))
            {
                tile.SetState(incorrectState);
            }
        }

        // 2. Есть в слове, но не там
        for (int i = 0; i < row.tiles.Length; i++)
        {
            Tile tile = row.tiles[i];

            if (tile.state != correctState && tile.state != incorrectState)
            {
                if (remaining.Contains(tile.letter))
                {
                    tile.SetState(wrongSpotState);
                    int idx = remaining.IndexOf(tile.letter);
                    remaining = remaining.Remove(idx, 1).Insert(idx, " ");
                }
                else
                {
                    tile.SetState(incorrectState);
                }
            }
        }

        if (HasWon(row)) {
    #if UNITY_IOS || UNITY_ANDROID
            CloseKeyboard();
    #endif
            enabled = false;
        }

        rowIndex++;
        columnIndex = 0;

        if (rowIndex >= rows.Length) {
    #if UNITY_IOS || UNITY_ANDROID
            CloseKeyboard();
    #endif
            enabled = false;
        }
    }

    private bool HasWon(Row row)
    {
        foreach (var t in row.tiles)
            if (t.state != correctState) return false;
        return true;
    }

    private void ClearBoard()
    {
        foreach (var r in rows)
            foreach (var t in r.tiles)
            {
                t.SetLetter('\0');
                t.SetState(emptyState);
            }
        rowIndex = 0;
        columnIndex = 0;
    }
    #endregion
}
