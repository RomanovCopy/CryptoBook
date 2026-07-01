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

        private PasswordBox? _passwordBox;
        private PasswordBox? _repeatPasswordBox;
        private TextBlock? _errorText;
        private System.Windows.Controls.Button? _okButton;

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

            _okButton.Click += OnOkClick;
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

        private bool ValidatePasswordBoxes()
        {
            if(_passwordBox!.SecurePassword.Length == 0)
            {
                _errorText!.Text = "Введите ключ.";
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

        private T FindRequired<T>(string name)
            where T : FrameworkElement
        {
            var element = AssociatedObject.FindName(name) as T;

            if(element == null)
                throw new InvalidOperationException(
                    $"Элемент '{name}' типа {typeof(T).Name} не найден.");

            return element;
        }

        private void DetachButton()
        {
            if(_okButton != null)
                _okButton.Click -= OnOkClick;

            _okButton = null;
        }

        private void ClearPasswordBoxes()
        {
            _passwordBox?.Clear();
            _repeatPasswordBox?.Clear();
        }
    }
}
