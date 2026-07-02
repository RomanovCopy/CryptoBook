using CryptoBook.Interfaces;
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
        public static readonly DependencyProperty KeyProviderProperty = DependencyProperty.Register(
                nameof(KeyProvider), typeof(IKeyProvider),typeof(KeyInputWindowBehavior));

        public static readonly DependencyProperty ConverterProperty = DependencyProperty.Register(
                nameof(Converter), typeof(ISecureStringConverter), typeof(KeyInputWindowBehavior));

        public static readonly DependencyProperty PasswordBoxNameProperty = DependencyProperty.Register(
                nameof(PasswordBoxName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("PasswordBox"));

        public static readonly DependencyProperty RepeatPasswordBoxNameProperty = DependencyProperty.Register(
                nameof(RepeatPasswordBoxName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("RepeatPasswordBox"));

        public static readonly DependencyProperty ErrorTextBlockNameProperty = DependencyProperty.Register(
                nameof(ErrorTextBlockName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("ErrorText"));

        public static readonly DependencyProperty OkButtonNameProperty = DependencyProperty.Register(
                nameof(OkButtonName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("OkButton"));

        public static readonly DependencyProperty CancelButtonNameProperty = DependencyProperty.Register(
                nameof(CancelButtonName), typeof(string), typeof(KeyInputWindowBehavior), new PropertyMetadata("CancelButton"));

        public static readonly DependencyProperty MinLengthProperty = DependencyProperty.Register(
        nameof(MinLength), typeof(int), typeof(KeyInputWindowBehavior), new PropertyMetadata(8));

        public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(
                nameof(MaxLength), typeof(int), typeof(KeyInputWindowBehavior), new PropertyMetadata(128));

        public static readonly DependencyProperty AllowWhiteSpaceProperty = DependencyProperty.Register(
                nameof(AllowWhiteSpace), typeof(bool), typeof(KeyInputWindowBehavior), new PropertyMetadata(false));

        public IKeyProvider? KeyProvider
        {
            get => (IKeyProvider?)GetValue(KeyProviderProperty);
            set => SetValue(KeyProviderProperty, value);
        }

        public ISecureStringConverter? Converter
        {
            get => (ISecureStringConverter?)GetValue(ConverterProperty);
            set => SetValue(ConverterProperty, value);
        }

        public string PasswordBoxName
        {
            get => (string)GetValue(PasswordBoxNameProperty);
            set => SetValue(PasswordBoxNameProperty, value);
        }

        public string RepeatPasswordBoxName
        {
            get => (string)GetValue(RepeatPasswordBoxNameProperty);
            set => SetValue(RepeatPasswordBoxNameProperty, value);
        }

        public string ErrorTextBlockName
        {
            get => (string)GetValue(ErrorTextBlockNameProperty);
            set => SetValue(ErrorTextBlockNameProperty, value);
        }

        public string OkButtonName
        {
            get => (string)GetValue(OkButtonNameProperty);
            set => SetValue(OkButtonNameProperty, value);
        }

        public string CancelButtonName
        {
            get => (string)GetValue(CancelButtonNameProperty);
            set => SetValue(CancelButtonNameProperty, value);
        }

        public int MinLength
        {
            get => (int)GetValue(MinLengthProperty);
            set => SetValue(MinLengthProperty, value);
        }

        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

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

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Closed += OnClosed;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Closed -= OnClosed;

            DetachButton();

            base.OnDetaching();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _passwordBox = FindRequired<PasswordBox>(PasswordBoxName);
            _repeatPasswordBox = FindRequired<PasswordBox>(RepeatPasswordBoxName);
            _errorText = FindRequired<TextBlock>(ErrorTextBlockName);
            _okButton = FindRequired<System.Windows.Controls.Button>(OkButtonName);
            _cancelButton = FindRequired<System.Windows.Controls.Button>(CancelButtonName);


            _okButton.Click += OnOkClick;
            _cancelButton.Click += OnCancelClick;
            _passwordBox.PreviewTextInput += OnPasswordTextInput;
            _repeatPasswordBox.PreviewTextInput += OnPasswordTextInput;

            System.Windows.DataObject.AddPastingHandler( _passwordBox, OnPasswordPaste);
            System.Windows.DataObject.AddPastingHandler( _repeatPasswordBox, OnPasswordPaste);

            _passwordBox.Focus();
        }


        private void OnClosed(object? sender, EventArgs e)
        {
            ClearPasswordBoxes();
            DetachButton();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if(KeyProvider == null)
                throw new InvalidOperationException("IKeyProvider не задан.");

            if(Converter == null)
                throw new InvalidOperationException("ISecureStringConverter не задан.");

            if(_passwordBox == null ||
                _repeatPasswordBox == null ||
                _errorText == null)
            {
                throw new InvalidOperationException("Окно не инициализировано.");
            }

            _errorText.Text = string.Empty;

            if(!ValidatePasswordBoxes())
                return;

            char[]? password = null;

            try
            {
                password = Converter.ToCharArray(
                    _passwordBox.SecurePassword);

                KeyProvider.SetKey(password);

                AssociatedObject.DialogResult = true;
                AssociatedObject.Close();
            } finally
            {
                if(password != null)
                    CryptographicOperations.ZeroMemory(
                        MemoryMarshal.AsBytes(password.AsSpan()));

                ClearPasswordBoxes();
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            ClearPasswordBoxes();
            AssociatedObject.DialogResult = false;
            AssociatedObject.Close();
        }

        private void OnPasswordTextInput( object sender, TextCompositionEventArgs e)
        {
            if(sender is not PasswordBox passwordBox)
                return;

            if(!CanInput(passwordBox, e.Text))
                e.Handled = true;
        }

        private void OnPasswordPaste( object sender, DataObjectPastingEventArgs e)
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

        private bool CanInput( PasswordBox passwordBox, string text)
        {
            if(passwordBox.SecurePassword.Length + text.Length > MaxLength)
                return false;

            foreach(char ch in text)
            {
                if(char.IsControl(ch))
                    return false;

                if(!AllowWhiteSpace && char.IsWhiteSpace(ch))
                    return false;
            }

            return true;
        }

        private bool ValidatePasswordBoxes()
        {
            int length = _passwordBox!.SecurePassword.Length;

            if(length == 0)
            {
                _errorText!.Text = "Введите ключ.";
                return false;
            }

            if(length < MinLength)
            {
                _errorText!.Text = $"Минимальная длина ключа: {MinLength}.";
                return false;
            }

            if(_repeatPasswordBox!.SecurePassword.Length == 0)
            {
                _errorText!.Text = "Повторите ключ.";
                return false;
            }

            bool equals = Converter!.ContentEquals(
                _passwordBox.SecurePassword,
                _repeatPasswordBox.SecurePassword);

            if(!equals)
            {
                _errorText!.Text = "Ключи не совпадают.";
                return false;
            }

            return true;
        }

        private T FindRequired<T>(string name) where T : FrameworkElement
        {
            var element = AssociatedObject.FindName(name) as T;

            if(element == null)
                throw new InvalidOperationException( $"Элемент '{name}' типа {typeof(T).Name} не найден.");

            return element;
        }

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

        private void ClearPasswordBoxes()
        {
            _passwordBox?.Clear();
            _repeatPasswordBox?.Clear();
        }
    }
}
