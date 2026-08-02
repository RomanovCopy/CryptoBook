using CryptoBook.Interfaces;
using CryptoBook.Infrastructure;
using CryptoBook.Security;
using CryptoBook.Views;

using Microsoft.Xaml.Behaviors;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.Behaviors
{
    public sealed class KeyInputWindowBehavior: Behavior<Window>
    {
        /// <summary>
        /// DependencyProperty для поставщика ключа (IKeyProvider).
        /// </summary>
        public static readonly DependencyProperty KeyProviderProperty = DependencyProperty.Register(
                nameof(KeyProvider), typeof(IKeyProvider), typeof(KeyInputWindowBehavior));

        /// <summary>
        /// DependencyProperty для конвертера SecureString в массив символов.
        /// </summary>
        public static readonly DependencyProperty ConverterProperty = DependencyProperty.Register(
                nameof(Converter), typeof(ISecureStringConverter), typeof(KeyInputWindowBehavior));

        /// <summary>
        /// DependencyProperty для имени PasswordBox в окне.
        /// </summary>
        public static readonly DependencyProperty PasswordBoxNameProperty = DependencyProperty.Register(
                nameof(PasswordBoxName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("PasswordBox"));

        /// <summary>
        /// DependencyProperty для имени второго поля повтора пароля в окне.
        /// </summary>
        public static readonly DependencyProperty RepeatPasswordBoxNameProperty = DependencyProperty.Register(
                nameof(RepeatPasswordBoxName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("RepeatPasswordBox"));

        /// <summary>
        /// DependencyProperty для имени TextBlock, в который выводятся ошибки валидации.
        /// </summary>
        public static readonly DependencyProperty ErrorTextBlockNameProperty = DependencyProperty.Register(
                nameof(ErrorTextBlockName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("ErrorText"));

        /// <summary>
        /// DependencyProperty для имени кнопки подтверждения (Ok).
        /// </summary>
        public static readonly DependencyProperty OkButtonNameProperty = DependencyProperty.Register(
                nameof(OkButtonName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("OkButton"));

        /// <summary>
        /// DependencyProperty для имени кнопки отмены.
        /// </summary>
        public static readonly DependencyProperty CancelButtonNameProperty = DependencyProperty.Register(
                nameof(CancelButtonName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("CancelButton"));

        /// <summary>
        /// DependencyProperty для минимальной длины ключа.
        /// </summary>
        public static readonly DependencyProperty MinLengthProperty = DependencyProperty.Register(
        nameof(MinLength), typeof(int), typeof(KeyInputWindowBehavior), new PropertyMetadata(8));

        /// <summary>
        /// DependencyProperty для максимальной длины ключа.
        /// </summary>
        public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(
                nameof(MaxLength), typeof(int), typeof(KeyInputWindowBehavior), new PropertyMetadata(128));

        /// <summary>
        /// DependencyProperty, указывающее, разрешать ли пробельные символы в ключе.
        /// </summary>
        public static readonly DependencyProperty AllowWhiteSpaceProperty = DependencyProperty.Register(
                nameof(AllowWhiteSpace), typeof(bool), typeof(KeyInputWindowBehavior), new PropertyMetadata(false));

        /// <summary>
        /// Поставщик ключа, реализующий IKeyProvider. Используется для передачи введённого ключа в приложение.
        /// </summary>
        public IKeyProvider? KeyProvider
        {
            get => (IKeyProvider?)GetValue(KeyProviderProperty);
            set => SetValue(KeyProviderProperty, value);
        }

        /// <summary>
        /// Конвертер SecureString в массив символов и обратно.
        /// </summary>
        public ISecureStringConverter? Converter
        {
            get => (ISecureStringConverter?)GetValue(ConverterProperty);
            set => SetValue(ConverterProperty, value);
        }

        /// <summary>
        /// Имя PasswordBox в окне, откуда берётся ключ.
        /// </summary>
        public string PasswordBoxName
        {
            get => (string)GetValue(PasswordBoxNameProperty);
            set => SetValue(PasswordBoxNameProperty, value);
        }

        /// <summary>
        /// Имя поля повторного ввода ключа в окне.
        /// </summary>
        public string RepeatPasswordBoxName
        {
            get => (string)GetValue(RepeatPasswordBoxNameProperty);
            set => SetValue(RepeatPasswordBoxNameProperty, value);
        }

        /// <summary>
        /// Имя TextBlock для отображения сообщений об ошибках валидации.
        /// </summary>
        public string ErrorTextBlockName
        {
            get => (string)GetValue(ErrorTextBlockNameProperty);
            set => SetValue(ErrorTextBlockNameProperty, value);
        }

        /// <summary>
        /// Имя кнопки подтверждения (Ok) в окне.
        /// </summary>
        public string OkButtonName
        {
            get => (string)GetValue(OkButtonNameProperty);
            set => SetValue(OkButtonNameProperty, value);
        }

        /// <summary>
        /// Имя кнопки отмены в окне.
        /// </summary>
        public string CancelButtonName
        {
            get => (string)GetValue(CancelButtonNameProperty);
            set => SetValue(CancelButtonNameProperty, value);
        }

        /// <summary>
        /// Минимально допустимая длина ключа.
        /// </summary>
        public int MinLength
        {
            get => (int)GetValue(MinLengthProperty);
            set => SetValue(MinLengthProperty, value);
        }

        /// <summary>
        /// Максимально допустимая длина ключа.
        /// </summary>
        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        /// <summary>
        /// Указывает, разрешены ли пробельные символы в ключе.
        /// </summary>
        public bool AllowWhiteSpace
        {
            get => (bool)GetValue(AllowWhiteSpaceProperty);
            set => SetValue(AllowWhiteSpaceProperty, value);
        }

        private PasswordBox? _passwordBox;
        private PasswordBox? _repeatPasswordBox;
        private TextBlock? _errorText;
        private System.Windows.Controls.Button? _okButton;
        private System.Windows.Controls.Button? _cancelButton;

        /// <summary>
        /// Вызывается при присоединении поведения к окну. Подписывается на события окна.
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Closed += OnClosed;
        }

        /// <summary>
        /// Вызывается при отсоединении поведения от окна. Снимает подписки и очищает обработчики.
        /// </summary>
        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Closed -= OnClosed;

            DetachButton();

            base.OnDetaching();
        }

        /// <summary>
        /// Обработчик события Loaded окна. Находит элементы по именам и подписывается на их события.
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _passwordBox = FindRequired<PasswordBox>(PasswordBoxName);
            _repeatPasswordBox = FindRequired<PasswordBox>(RepeatPasswordBoxName);
            _errorText = FindRequired<TextBlock>(ErrorTextBlockName);
            _okButton = FindRequired<System.Windows.Controls.Button>(OkButtonName);
            _cancelButton = FindRequired<System.Windows.Controls.Button>(CancelButtonName);

            _passwordBox.IsEnabled = true;
            _repeatPasswordBox.IsEnabled= false;
            _okButton.Click += OnOkClick;
            _cancelButton.Click += OnCancelClick;
            _passwordBox.PreviewTextInput += OnPasswordTextInput;
            _repeatPasswordBox.PreviewTextInput += OnPasswordTextInput;
            _passwordBox.PreviewKeyDown += OnPasswordBoxPreviewKeyDown;
            _repeatPasswordBox.PreviewKeyDown += OnPasswordBoxPreviewKeyDown;

            System.Windows.DataObject.AddPastingHandler(_passwordBox, OnPasswordPaste);
            System.Windows.DataObject.AddPastingHandler(_repeatPasswordBox, OnPasswordPaste);

            _passwordBox.Focus();
        }


        /// <summary>
        /// Обработчик события Closed окна. Очищает поля и снимает обработчики.
        /// </summary>
        private void OnClosed(object? sender, EventArgs e)
        {
            ClearPasswordBoxes();
            DetachButton();
            DetachPasswordBoxes();
        }

        /// <summary>
        /// Обработчик клика по кнопке Ok. Пытается принять введённый ключ.
        /// </summary>
        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            TryAccept();
        }

        /// <summary>
        /// Обработчик клика по кнопке отмены. Очищает поля и закрывает окно с результатом false.
        /// </summary>
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            ClearPasswordBoxes();
            AssociatedObject.DialogResult = false;
            AssociatedObject.Close();
        }

        /// <summary>
        /// Обработчик ввода текста в PasswordBox. Проверяет допустимость вводимых символов.
        /// </summary>
        private void OnPasswordTextInput(object sender, TextCompositionEventArgs e)
        {
            if(sender is not PasswordBox passwordBox)
                return;

            if(!CanInput(passwordBox, e.Text))
                e.Handled = true;
            else
                e.Handled = false;
        }

        /// <summary>
        /// Обработчик вставки в PasswordBox. Проверяет вставляемый текст на допустимость.
        /// </summary>
        private void OnPasswordPaste(object sender, DataObjectPastingEventArgs e)
        {
            if(sender is not PasswordBox passwordBox)
            {
                e.CancelCommand();
                return;
            }

            if(!e.DataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) as string;

            if(string.IsNullOrEmpty(text) || !CanInput(passwordBox, text))
                e.CancelCommand();
        }

        /// <summary>
        /// Обработчик нажатия клавиш в PasswordBox. Обрабатывает Enter/Return для перехода или подтверждения.
        /// </summary>
        private void OnPasswordBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key != Key.Enter && e.Key != Key.Return)
            {
                if(e.Key == Key.Space && !AllowWhiteSpace)
                {
                    e.Handled = true;
                }
                return;
            }

            e.Handled = true;

            if(ReferenceEquals(sender, _passwordBox))
            {
                if(_passwordBox.Password.Length < MinLength)
                {
                _errorText!.Text = LocalizationManager.Format(
                    "Key.TooShort",
                    MinLength);
                    return;
                }
                _errorText!.Text = string.Empty;
                _passwordBox!.IsEnabled = false;
                _repeatPasswordBox!.IsEnabled = true;
                _repeatPasswordBox?.Focus();
                _repeatPasswordBox?.SelectAll();
                return;
            }

            if(ReferenceEquals(sender, _repeatPasswordBox))
                TryAccept();
        }

        /// <summary>
        /// Пытается принять введённый ключ: валидирует, конвертирует и передаёт в IKeyProvider.
        /// В случае успеха закрывает окно с DialogResult = true.
        /// </summary>
        private void TryAccept()
        {
            if(KeyProvider == null)
                throw new InvalidOperationException("IKeyProvider не задан.");

            if(Converter == null)
                throw new InvalidOperationException("ISecureStringConverter не задан.");

            if(_passwordBox == null || _repeatPasswordBox == null || _errorText == null)
            {
                throw new InvalidOperationException("Окно не инициализировано.");
            }

            _errorText.Text = string.Empty;

            if(!ValidatePasswordBoxes())
                return;

            char[]? password = null;

            try
            {
                password = Converter.ToCharArray(_passwordBox.SecurePassword);

                KeyProvider.SetKey(password);

                AssociatedObject.DialogResult = true;
                AssociatedObject.Close();
            } finally
            {
                if(password != null)
                {
                    CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
                }

                ClearPasswordBoxes();
            }
        }

        /// <summary>
        /// Проверяет, допустим ли вводимый текст в конкретный PasswordBox по длине и по запрещённым символам.
        /// </summary>
        private bool CanInput(PasswordBox passwordBox, string text)
        {
            if(passwordBox.SecurePassword.Length + text.Length > MaxLength)
                return false;

            foreach(char ch in text)
            {
                if(char.IsControl(ch) || ch == ' ' && !AllowWhiteSpace)
                {
                _errorText!.Text = LocalizationManager.GetString(
                    "Key.InvalidCharacters");
                    return false;
                }
            }
            _errorText!.Text =string.Empty;
            return true;
        }

        /// <summary>
        /// Выполняет валидацию полей пароля: длина, присутствие и совпадение двух полей.
        /// Возвращает true, если валидация успешна, иначе выводит сообщение в _errorText и возвращает false.
        /// </summary>
        private bool ValidatePasswordBoxes()
        {
            int length = _passwordBox!.SecurePassword.Length;

            if(length == 0)
            {
                _errorText!.Text = LocalizationManager.GetString("Key.Enter");
                return false;
            }

            if(length < MinLength)
            {
                _errorText!.Text = LocalizationManager.Format(
                    "Key.MinimumLength",
                    MinLength);
                return false;
            }

            if(_repeatPasswordBox!.SecurePassword.Length == 0)
            {
                _errorText!.Text = LocalizationManager.GetString("Key.Repeat");
                return false;
            }

            bool equals = Converter!.ContentEquals(_passwordBox.SecurePassword, _repeatPasswordBox.SecurePassword);

            if(!equals)
            {
                _errorText!.Text = LocalizationManager.GetString(
                    "Key.Mismatch");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Находит элемент в окне по имени и приводит его к типу T. Если элемент не найден, бросает InvalidOperationException.
        /// </summary>
        private T FindRequired<T>(string name) where T : FrameworkElement
        {
            var element = AssociatedObject.FindName(name) as T;

            if(element == null)
                throw new InvalidOperationException($"Элемент '{name}' типа {typeof(T).Name} не найден.");

            return element;
        }

        /// <summary>
        /// Снимает обработчики событий с кнопок и связанных полей ввода. Освобождает ссылки на кнопки.
        /// </summary>
        private void DetachButton()
        {
            if(_okButton != null)
                _okButton.Click -= OnOkClick;
            if(_cancelButton != null)
                _cancelButton.Click -= OnCancelClick;
            if(_passwordBox != null)
            {
                _passwordBox.PreviewTextInput -= OnPasswordTextInput;
                System.Windows.DataObject.RemovePastingHandler(_passwordBox, OnPasswordPaste);
            }

            if(_repeatPasswordBox != null)
            {
                _repeatPasswordBox.PreviewTextInput -= OnPasswordTextInput;
                System.Windows.DataObject.RemovePastingHandler(_repeatPasswordBox, OnPasswordPaste);
            }

            _okButton = null;
            _cancelButton = null;
        }

        /// <summary>
        /// Снимает обработчики событий с полей ввода пароля (PasswordBox).
        /// </summary>
        private void DetachPasswordBoxes()
        {
            if(_passwordBox != null)
            {
                _passwordBox.PreviewTextInput -= OnPasswordTextInput;
                _passwordBox.PreviewKeyDown -= OnPasswordBoxPreviewKeyDown;

                System.Windows.DataObject.RemovePastingHandler(_passwordBox, OnPasswordPaste);
            }

            if(_repeatPasswordBox != null)
            {
                _repeatPasswordBox.PreviewTextInput -= OnPasswordTextInput;
                _repeatPasswordBox.PreviewKeyDown -= OnPasswordBoxPreviewKeyDown;

                System.Windows.DataObject.RemovePastingHandler(_repeatPasswordBox, OnPasswordPaste);
            }
        }

        /// <summary>
        /// Очищает содержимое обоих полей ввода пароля.
        /// </summary>
        private void ClearPasswordBoxes()
        {
            _passwordBox?.Clear();
            _repeatPasswordBox?.Clear();
        }
    }
}
