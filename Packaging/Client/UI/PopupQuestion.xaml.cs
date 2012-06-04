//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Client.UI {
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;

    /// <summary>
    ///   Interaction logic for PopupQuestion.xaml
    /// </summary>
    public partial class PopupQuestion : Window {
        public string QuestionText { get; set; }
        public string NegativeText { get; set; }
        public string PositiveText { get; set; }

        public PopupQuestion(string text, string negative, string positive) {
            QuestionText = text;
            NegativeText = negative;
            PositiveText = positive;

            InitializeComponent();

            MessageText.SetBinding(TextBlock.TextProperty, new Binding("QuestionText") {Source = this});
            CancelText.SetBinding(TextBlock.TextProperty, new Binding("NegativeText") {Source = this});
            ContinueText.SetBinding(TextBlock.TextProperty, new Binding("PositiveText") {Source = this});
            Loaded += (o, e) => {
                Topmost = false;
            };
        }

        private void NegativeButtonClick(object sender, RoutedEventArgs e) {
            // cancel the request.
            DialogResult = false;
            Close();
        }

        private void PositiveButtonClick(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }
    }
}