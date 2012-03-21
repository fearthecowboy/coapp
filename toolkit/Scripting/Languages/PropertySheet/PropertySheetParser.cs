//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using System.IO;

namespace CoApp.Toolkit.Scripting.Languages.PropertySheet {
    using System.Collections.Generic;
    using Exceptions;
    using Extensions;
    using Utility;

    public class SourceLocation {
        public int Row;
        public int Column;
        public string SourceFile;
    }

    public class PropertySheetParser {
        private readonly string _propertySheetText;
        private readonly string _filename;
        private readonly PropertySheet _propertySheet;

        protected PropertySheetParser(string text, string originalFilename,PropertySheet propertySheet) {
            _propertySheetText = text;
            _propertySheet = propertySheet;
            _filename = originalFilename;
        }

        protected static Token? SkipToNext(ref List<Token>.Enumerator enumerator) {
            Token? token;

            do {
                if (!enumerator.MoveNext())
                    return null;

                token = enumerator.Current;

                switch (token.Value.Type) {
                        // regardless where we are, skip over whitespace, etc.
                    case TokenType.WhiteSpace:
                    case TokenType.LineComment:
                    case TokenType.MultilineComment:
                        continue;
                }
                break;
            } while (true);
            return token;
        }

        protected PropertySheet Parse() {
            var tokenStream = PropertySheetTokenizer.Tokenize(_propertySheetText);
            var state = ParseState.Global;
            var enumerator = tokenStream.GetEnumerator();
            var importFilename = string.Empty;

            Token token;

            Rule rule = null;
            string ruleName = "*";
            string ruleParameter = null;
            string ruleClass = null;
            string ruleId = null;

            NewRuleProperty property = null;

            var sourceLocation = new SourceLocation {
                Row=0,
                Column = 0,
                SourceFile = null,
            };

            string propertyName = null;
            string propertyLabelText = null;
            string presentlyUnknownValue = null;
            string collectionName = null;

            enumerator.MoveNext();

            do {
                token = enumerator.Current;

                switch (token.Type) {
                        // regardless where we are, skip over whitespace, etc.
                    case TokenType.WhiteSpace:
                    case TokenType.LineComment:
                    case TokenType.MultilineComment:
                        continue;
                }

                // System.Console.WriteLine("Token : {0} == [{1}]", token.Type, token.Data);

                switch (state) {
                    case ParseState.Global:
                        sourceLocation = new SourceLocation { // will be the start of the next new rule.
                            Row = token.Row,
                            Column = token.Column,
                            SourceFile = _filename,
                        };

                        switch (token.Type) {
                            case TokenType.Identifier: // look for identifier as the start of a selector
                                if( token.Data == "@import" ) {
                                    // special case to handle @import rules
                                    state = ParseState.Import;
                                    continue;
                                }
                                state = ParseState.Selector;
                                ruleName = token.Data;
                                continue;

                            case TokenType.Dot:
                                state = ParseState.SelectorDot;
                                ruleName = "*";
                                // take next identifier as the classname
                                continue;

                            case TokenType.Pound:
                                state = ParseState.SelectorPound;
                                ruleName = "*";
                                // take next identifier as the id
                                continue;

                            case TokenType.Semicolon: // tolerate extra semicolons.
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 100", "Expected one of '.' , '#' or identifier");
                        }

                    case ParseState.Import :
                        switch (token.Type) {
                            case TokenType.StringLiteral:
                            case TokenType.Identifier:
                                state = ParseState.ImportFilename;
                                importFilename = token.Data;
                                continue;
                            default:
                                throw new EndUserParseException(token, _filename, "PSP 121", "Expected a string literal for filename (Missing semicolon?)");
                        }

                    case ParseState.ImportFilename:
                        switch (token.Type) {
                            case TokenType.Semicolon:
                                // import the specified file and insert it's rules into this sheet.
                                string document = null;
                                string filename = null;

                                if (!string.IsNullOrEmpty(_filename)) {
                                    // it's either a full path to a file, or a relative path to the current document.
                                    try {
                                        var folder = Path.GetDirectoryName(_filename.GetFullPath());
                                        filename = Path.Combine(folder, importFilename);
                                        if (File.Exists(filename)) {
                                            document = File.ReadAllText(filename);
                                        }
                                    } catch {
                                    } // hmm that didn't work. I guess just try the filename...
                                }

                                if( document == null ) {
                                    // without a filename for the current document, all we can do is hope that there is a file at the specified string

                                    filename = importFilename.GetFullPath();
                                    if (!File.Exists(filename)) {
                                        throw new EndUserParseException(token, _filename, "PSP 122", "Imported file '{0}' not found", filename);
                                    }
                                    document = File.ReadAllText(filename);
                                }

                                // parse the contents of that file into the current property sheet.
                                new PropertySheetParser(document, filename, _propertySheet).Parse();
                                state = ParseState.Global;
                                continue;
                            default:
                                throw new EndUserParseException(token, _filename, "PSP 121", "Expected a string literal for filename");
                        }

                    case ParseState.Selector:
                        switch (token.Type) {
                            case TokenType.Dot:
                                state = ParseState.SelectorDot;
                                continue;

                            case TokenType.Pound:
                                state = ParseState.SelectorPound;
                                continue;

                            case TokenType.SelectorParameter:
                                ruleParameter = token.Data;
                                if( ruleParameter.IndexOfAny("\r\n".ToCharArray()) >= 0) {
                                    throw new EndUserParseException(token, _filename, "PSP 123", "Selector parameter may not contain CR/LFs (missing close bracket?): {0} ", Rule.CreateSelectorString(ruleName, ruleParameter,ruleClass, ruleId ));     
                                }
                                continue;

                            case TokenType.OpenBrace:
                                state = ParseState.InRule;

                                // property sheets now merge rules when redefined.
                                // if( _propertySheet.HasRule(ruleName, ruleParameter, ruleClass, ruleId) ) {
                                   // throw new EndUserParseException(token, _filename, "PSP 113", "Duplicate rule with identical selector not allowed: {0} ", Rule.CreateSelectorString(ruleName, ruleParameter,ruleClass, ruleId )); 
                                // }

                                rule = _propertySheet.GetRule(ruleName, ruleParameter, ruleClass, ruleId);
                                
                                ruleName = null;
                                ruleParameter = null;
                                ruleClass = null;
                                ruleId = null;

                                rule.SourceLocation = sourceLocation;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 101", "Expected one of '.' , '#' , '[' or '{{' ." );
                        }

                    case ParseState.SelectorDot:
                        switch (token.Type) {
                            case TokenType.Identifier: 
                                ruleClass = token.Data;
                                state = ParseState.Selector;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 102", "Expected identifier");
                        }

                    case ParseState.SelectorPound:
                        switch (token.Type) {
                            case TokenType.Identifier:
                                ruleId = token.Data;
                                state = ParseState.Selector;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 103", "Expected identifier");
                        }

                    case ParseState.InRule:
                        switch (token.Type) {
                            case TokenType.Semicolon: // extra semicolons are tolerated.
                                continue;

                            case TokenType.StringLiteral:
                            case TokenType.Identifier:
                                propertyName = token.Data;
                                state = ParseState.HavePropertyName;
                                sourceLocation = new SourceLocation {
                                    Row = token.Row,
                                    Column = token.Column,
                                    SourceFile = _filename,
                                };
                                
                                continue;

                            case TokenType.CloseBrace: 
                                // this rule is DONE.
                                rule = null; // set this to null, so that we don't accidentally add new stuff to this rule.
                                state = ParseState.Global;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 104", "In rule, expected semi-colon ';', close-brace '}}' or value." );
                        }

                    case ParseState.HavePropertyName:
                        switch (token.Type) {
                            case TokenType.Colon:
                                state = ParseState.HavePropertySeparator;
                                property = rule.GetRuleProperty(propertyName);
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 105", "Found rule property name, expected colon ':'." );
                        }

                    case ParseState.HavePropertySeparator:
                        switch (token.Type) {
                            case TokenType.StringLiteral:
                            case TokenType.NumericLiteral:
                                state = ParseState.HavePropertyLabel;
                                if ("@Literal" == token.RawData) {
                                    propertyLabelText = token.Data;
                                }
                                else {
                                    propertyLabelText = token.Data;
                                }
                                continue;

                            case TokenType.Identifier:
                                state = ParseState.HavePropertyLabel;
                                propertyLabelText = token.Data;
                                continue;

                            case TokenType.OpenBrace:
                                state = ParseState.InPropertyCollectionWithoutLabel;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 106", "After rule property name, expected value, open-brace '{{' or open-parenthesis '('." );
                        }

                    case ParseState.InPropertyCollectionWithoutLabel:
                        switch (token.Type) {
                            case TokenType.StringLiteral:
                            case TokenType.NumericLiteral:
                            case TokenType.Identifier:
                                // at this point it could be a collection, a label, or a value.
                                presentlyUnknownValue = token.Data;
                                state = ParseState.InPropertyCollectionWithoutLableButHaveSomething;
                                continue;

                            case TokenType.CloseBrace:
                                state = ParseState.HavePropertyCompleted;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 107", "In property collection, expected value or close brace '}}'");
                        }

                    case ParseState.InPropertyCollectionWithoutLableButHaveSomething:
                        switch (token.Type) {
                            case TokenType.Lambda:
                                collectionName = presentlyUnknownValue;
                                presentlyUnknownValue = null;
                                state = ParseState.HasLambda;
                                continue;

                            case TokenType.Equal:
                                // looks like it's gonna be a label = value type.
                                propertyLabelText = presentlyUnknownValue;
                                presentlyUnknownValue = null;
                                state = ParseState.HasEqualsInCollection;
                                continue;

                            case TokenType.Comma: {
                                    // turns out its a simple collection item.
                                    var pv = property.GetPropertyValue(string.Empty);
                                    pv.Add(presentlyUnknownValue);
                                    pv.SourceLocation = sourceLocation;
                                    presentlyUnknownValue = null;
                                    state = ParseState.InPropertyCollectionWithoutLabel;
                                }
                                continue;

                            case TokenType.CloseBrace: {
                                    // turns out its a simple collection item.
                                    var pv = property.GetPropertyValue(string.Empty);
                                    pv.Add(presentlyUnknownValue);
                                    pv.SourceLocation = sourceLocation;
                                    presentlyUnknownValue = null;
                                    state = ParseState.HavePropertyCompleted;
                                }
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 114", "after an value or identifier in a collection expected a '=>' or '=' or ',' .");
                        }

                    case  ParseState.HasEqualsInCollection :
                        switch (token.Type) {
                            case TokenType.StringLiteral:
                            case TokenType.NumericLiteral:
                            case TokenType.Identifier: {
                                    var pv = property.GetPropertyValue(propertyLabelText);
                                    pv.Add(token.Data);
                                    pv.SourceLocation = sourceLocation;
                                    state = ParseState.InPropertyCollectionWithoutLabelWaitingForComma;
                                }
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 119", "after an equals '=' in a collection, expected a value or identifier.");

                        }
                    case ParseState.HasLambda:
                        switch (token.Type) {
                            case TokenType.StringLiteral:
                            case TokenType.NumericLiteral:
                            case TokenType.Identifier:
                                propertyLabelText = token.Data;
                                state = ParseState.HasLambdaAndLabel;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 115", "After the '=>' in a collection, expected value or identifier.");

                        }

                    case ParseState.HasLambdaAndLabel:
                        switch (token.Type) {
                            case TokenType.Equal:
                                state = ParseState.HasLambdaAndLabelAndEquals;
                                continue;

                            case TokenType.Semicolon:
                            case TokenType.Comma :
                            case TokenType.CloseBrace: {
                                // assumes "${DEFAULTLAMBDAVALUE}" for the lamda value
                                    var pv = property.GetPropertyValue(propertyLabelText, collectionName);
                                    pv.Add("${DEFAULTLAMBDAVALUE}");
                                    pv.SourceLocation = sourceLocation;
                                    collectionName = propertyLabelText = null;
                                    state = ParseState.InPropertyCollectionWithoutLabelWaitingForComma;
                                }   

                                if (token.Type == TokenType.CloseBrace) {
                                    // and, we're closing out this property.
                                    state = ParseState.HavePropertyCompleted;
                                }
                                continue;


                            default:
                                throw new EndUserParseException(token, _filename, "PSP 116", "After the '{0} => {1}' in collection, expected '=' ", collectionName, propertyLabelText);

                        }

                    case ParseState.HasLambdaAndLabelAndEquals:
                        switch (token.Type) {
                            case TokenType.StringLiteral:
                            case TokenType.NumericLiteral:
                            case TokenType.Identifier: {
                                    var pv = property.GetPropertyValue(propertyLabelText, collectionName);
                                    pv.Add(token.Data);
                                    pv.SourceLocation = sourceLocation;
                                    collectionName = propertyLabelText = null;
                                    state = ParseState.InPropertyCollectionWithoutLabelWaitingForComma;
                                }   
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 117", "After the '{0} => {1} = ' in collection, expected a value or identifier" ,collectionName, propertyLabelText);
                        }

                    case ParseState.InPropertyCollectionWithoutLabelWaitingForComma:
                        switch (token.Type) {
                            case TokenType.Comma:
                            case TokenType.Semicolon:
                                state = ParseState.InPropertyCollectionWithoutLabel;
                                continue;

                            case TokenType.CloseBrace:
                                state = ParseState.HavePropertyCompleted;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 118", "After complete expression or value in a collection, expected ',' or '}'.");
                        }

                    case ParseState.InPropertyCollectionWithLabel:
                        switch (token.Type) {
                            case TokenType.StringLiteral:
                            case TokenType.NumericLiteral:
                            case TokenType.Identifier:
                                presentlyUnknownValue = token.Data;
                                state = ParseState.HaveCollectionValue;
                                continue;

                            case TokenType.CloseBrace:
                                state = ParseState.HavePropertyCompleted;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 107", "In property collection, expected value or close brace '}}'" );
                        }

                    case ParseState.HaveCollectionValue: 
                        switch (token.Type) {
                            case TokenType.Comma: {
                                    var pv = property.GetPropertyValue(propertyLabelText);
                                    pv.Add(presentlyUnknownValue);
                                    pv.SourceLocation = sourceLocation;
                                    collectionName = propertyLabelText = null;
                                    state = ParseState.InPropertyCollectionWithLabel;
                                }
                                continue;

                            case TokenType.CloseBrace: {
                                    var pv = property.GetPropertyValue(propertyLabelText);
                                    pv.Add(presentlyUnknownValue);
                                    pv.SourceLocation = sourceLocation;
                                    collectionName = propertyLabelText = null;
                                    state = ParseState.HavePropertyCompleted;
                                }
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 108", "With property collection value, expected comma ',' or close-brace '}}'.");
                        }

                    case ParseState.HavePropertyLabel:
                        switch (token.Type) {
                            case TokenType.Equal:
                                state = ParseState.HavePropertyEquals;
                                continue;

                            case TokenType.Dot:
                                var t = SkipToNext(ref enumerator);

                                if( !t.HasValue ) {
                                    throw new EndUserParseException(token, _filename, "PSP 109", "Unexpected end of Token stream [HavePropertyLabel]");
                                }
                                token = t.Value;
                                if (token.Type == TokenType.Identifier || token.Type == TokenType.NumericLiteral) {
                                    propertyLabelText += "." + token.Data;
                                }
                                else 
                                    throw new EndUserParseException(token, _filename, "PSP 110", "Expected identifier or numeric literal after Dot '.'.");
                                continue;

                            case TokenType.Semicolon: {
                                    // it turns out that what we thought the label was, is really the property value,
                                    // the label is an empty string
                                    var pv = property.GetPropertyValue(string.Empty);
                                    pv.Add(propertyLabelText);
                                    pv.SourceLocation = sourceLocation;
                                    propertyName = propertyLabelText = null;
                                    state = ParseState.InRule;
                                }
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 111", "After property value, expected semi-colon ';' or equals '='." );
                        }

                    case ParseState.HavePropertyEquals:
                        switch (token.Type) {
                            case TokenType.Identifier:
                            case TokenType.StringLiteral:
                            case TokenType.NumericLiteral: {
                                    // found our property-value. add it, and move along.
                                    var pv = property.GetPropertyValue(propertyLabelText);
                                    pv.Add(token.Data);
                                    pv.SourceLocation = sourceLocation;
                                    propertyName = propertyLabelText = null;
                                    state = ParseState.HavePropertyCompleted;
                                }
                                continue;

                            case TokenType.OpenBrace:
                                // we're starting a new collection (where we have the label already).
                                state = ParseState.InPropertyCollectionWithLabel;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 112", "After equals in property, expected value or close-brace '{B{'." );
                        }

                    case ParseState.HavePropertyCompleted:
                        switch (token.Type) {
                            case TokenType.Semicolon:
                                state = ParseState.InRule;
                                continue;

                            default:
                                throw new EndUserParseException(token, _filename, "PSP 113", "After property completed, expected semi-colon ';'.");
                        }

                    default:
                        throw new EndUserParseException(token, _filename, "PSP 120", "CATS AND DOGS, LIVINGTOGETHER...");
                }
            } while (enumerator.MoveNext());
            return _propertySheet;
        }

        public static PropertySheet Parse(string propertySheetText, string originalFilename, PropertySheet propertySheet = null ) {
            var p = new PropertySheetParser(propertySheetText,originalFilename,propertySheet ?? new PropertySheet());
            return p.Parse();
        }

        #region Nested type: ParseState

        private enum ParseState {
            Global,
            Selector,
            SelectorDot,
            SelectorPound,
            InRule,
            HavePropertyName,
            HavePropertySeparator,
            HavePropertyLabel,
            HavePropertyCompleted,
            HavePropertyEquals,
            InPropertyCollectionWithLabel,
            InPropertyCollectionWithoutLabel,
            HaveCollectionValue,

            InPropertyCollectionWithoutLableButHaveSomething,
            HasLambda,
            HasLambdaAndLabel,
            HasEqualsInCollection,
            HasLambdaAndLabelAndEquals,
            InPropertyCollectionWithoutLabelWaitingForComma,

            Import,
            ImportFilename,
        }

        #endregion
    }
}