using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    /*
        Objective for the parser:
            -index characters, and then tokens so that when there are parse errors line/col can be identified
            -form ASTs, and throw errors when they can't be correctly formed
    */

    public enum TokenType { Int, LParen, RParen, Id, Symbol, Bool }

    class Token
    {
        public int col, row;
        public string token;
        public TokenType type;

        public override string ToString()
        {
            //this is mostly just for debugging
            return String.Format("({3})R:{0} C:{1} Token:{2}", row, col, token, type);
        }

        public Token(int r, int c, string t)
        {
            //constructor deals with the token types, so when lexing, you just need to break on delimiters
            row = r; col = c; token = t;
            Regex numReg = new Regex(@"^\d+$"); //only support ints for the time being
            Regex symReg = new Regex(@"'[a-zA-Z0-9+-=/*]+");
            Regex idReg = new Regex(@"[a-zA-Z+-=/*][a-zA-Z0-9+-=/*]*");

            Match isNum = numReg.Match(t);
            Match isSym = symReg.Match(t);
            Match isID = idReg.Match(t);

            if (t == "(")
            {
                type = TokenType.LParen;
            } else if (t == ")")
            {
                type = TokenType.RParen;
            }
            else if (isNum.Success)
            {
                type = TokenType.Int;
            }else if(t == "#t" || t == "#f"){
                type = TokenType.Bool;
            }
            else if(isSym.Success)
            {
                type = TokenType.Symbol;
            }
            else if(isID.Success)
            {
                type = TokenType.Id;
            }
            else
            {
                throw new Exception(String.Format("Bad Token, probably an invalid character: {0}, Row: {1}, Col: {2}", t, row, col));
            }
        }
    }

    class DerpParser
    {

        public static List<SExpression> parseCode(string text)
        {
            return Parse(Tokenize(text));
        }

        public static List<Token> Tokenize(string text) {
            string toke = ""; int row = 0, col = 0, lrow = 0, lcol = 0; // lrow, lcol are indices of the left most character in the accumulated tokens, row/col are indices for current char
            List<Token> tokens = new List<Token>();
            string skippedDelims = " \n\r\t"; //these are delims that indicate breaks, but are NOT part of tokens themselves
            string includedDelims = "()"; //these indicate end of token, but are also tokens themselves
            
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i]; //c is what we are testing for end of token

                if (includedDelims.Contains(c)) 
                {
                    if (toke.Length > 0) //if the accumulated chars non empty add new token
                    {
                        tokens.Add(new Token(lrow, lcol, toke));
                        toke = ""; //reset token accumulater
                    }
                    tokens.Add(new Token(row, col, "" + c));//the delimeter is also a token, so create it right away
                    lrow = row; lcol = ++col; //start of our next token is the char to the right of our current char
                }

                else if (skippedDelims.Contains(c))
                {
                    if (toke.Length > 0) //if we've accumulated anything, store the token
                    {
                        tokens.Add(new Token(lrow, lcol, toke));
                        toke = "";
                    }

                    if (c == '\n') { row++; col = 0; } //if our delimiter was \n, advance to next row and reset col
                    else if(c!= '\r'){ col++; }
                    lrow = row; lcol = col;
                }
                else
                {
                    toke += c; //no delim? add to token accumulater
                    col++; //and move to the next column
                }
                
            }
            if (toke.Length > 0) //if we've gotten to the end of our input and we have chars in the accumulator, store token
                tokens.Add(new Token(lrow, lcol, toke));

            return tokens;
        }

        public static List<SExpression> Parse(List<Token> tokens)
        {
            //ParseSExpression is a side-effecty function:
            //  it removes enough tokens from the token list input to
            //  to form the next SExpression. So we just need to call it until its flushed the token list
            List<SExpression> parseTree = new List<SExpression>();
            while(tokens.Count > 0)
            {
                parseTree.Add(ParseSExpression(tokens));
            }
            return parseTree;
        }

        public static SExpression ParseSExpression(List<Token> tokens)
        {
            //this recursive buddy is pretty simple
            //  pop the top token and process. Most tokens just get converted to their associated SExpression type, but L/R Parens do goofy things.
            //  If it's an LParen, iterate through token list until an RParen is found, using self to convert tokens into SExpressions
            Token top = tokens.First(); //Pop!
            tokens.RemoveAt(0);         //Pop!

            switch (top.type) {
                case TokenType.Int: //pass through
                    return new SInt(Int32.Parse(top.token));
                case TokenType.Id: //pass through
                    return new SID(top.token);
                case TokenType.Bool: //pass through
                    return new SBool(top.token == "#f");
                case TokenType.Symbol: //pass through
                    return new SSymbol(top.token);
                case TokenType.LParen: //Need to grab all tokens until RParen to resolve LParen
                    List<SExpression> elms = new List<SExpression>();
                    
                    while (true)
                    {
                        if (tokens.First().type == TokenType.RParen)
                        {
                            elms.Add(new SList()); //Proper lists are terminated with empty list, so add that
                            tokens.RemoveAt(0); //pop the RParen off the stack
                            return new SList(elms); //We're done! Return parsed list!
                        }
                        elms.Add(ParseSExpression(tokens));  //Get SExpression for my non-RParen token
                    }
                case TokenType.RParen:  //RParens are caught by LParen loop, if we get here, that's bad
                    throw new Exception("Unexpected RParen\n" + top.ToString());
                default:  //Only happens if I add new token types, but don't add parsing logic for them
                    throw new Exception("Unknown Token Type"); 
            }
        }
    }
}
