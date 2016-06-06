using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    /*
        TODO:
            -Not tracking original tokens, needed for stack traces?
    */

    delegate SExpression Func(List<SExpression> args, Environment e);// Define type 'Func', anonymous function that operates on a list of args in an environment and produces an SExpression

    abstract class SExpression : ICloneable
    {
        public Token originalToken;

        public abstract object Clone();
        abstract public string DebugString();
    }
    abstract class SAtomic : SExpression { }
    abstract class SApplicable : SAtomic
    {
        abstract public SExpression apply(List<SExpression> args, Environment e);
    }

    class SPrimitive : SApplicable // SPrimitive is a function defined in C# by the interpreter
    {
        Func f;
        public SPrimitive(Func d) { f = d; }

        override public SExpression apply(List<SExpression> args, Environment e)
        {
            return f(args, e);
        }

        public override string ToString() { return "#primative"; }
        public override string DebugString() { return "<PRIMATIVE>";  }

        public override object Clone()
        {
            return new SPrimitive(f);
        }
    }

    class SFunc : SApplicable //SFuncs are lambda functions
    {
        private SID arglist;
        private Environment env; //private environment for sweet, sweet closures
        private List<SID> names; //names bound in definition
        private SExpression body; //body of execution

        public SFunc(List<SID> names, SExpression body, Environment e)
        {
            //our starting environment is the env we were defined in
            arglist = null;
            env = new Environment(e);
            foreach(SID id in names)
            {
                env.addVal(id.identifier, new SNone()); //add bound variables to environment with default None value
            }
            this.names = names;
            this.body = body;
        }

        public SFunc(SID arglst, SExpression body, Environment e)
        {
            arglist = arglst;
            names = null;

            //our starting environment is the env we were defined in
            env = new Environment(e);
            env.addVal(arglist.identifier, new SNone()); //add bound variables to environment with default None value
            
            this.body = body;
        }

        override public SExpression apply(List<SExpression> args, Environment e)
        {
            //There are two styles of arguments:
            //      -names for all args
            //      -single name, all args are a list

            //if arglist is null, verify number of args, eval them, and add them to the environment
            if (arglist == null)
            {
                if (args.Count != names.Count)
                    throw new Exception("Incorrect number of args");
                for (int i = 0; i < args.Count; i++)
                {
                    //evaluate all of the args and bind them to my local env according to their cooresponding values
                    env.setLocalVal(names[i].identifier, DerpScheme.DerpInterpreter.evaluate(args[i], e));
                }
            }
            else //otherwise, convert our args to an SList and bind them to environment
            {
                args.Add(new SList());
                SList argSList = new SList(args);
                env.setLocalVal(arglist.identifier, argSList);
            }

            //finally actually execute body
            return DerpScheme.DerpInterpreter.evaluate(body, env);
        }

        public override string ToString() { return "#procedure"; }
        public override string DebugString() {
            string rval = "<FUNC: ";
            if (arglist == null) //multiple arg names
            {
                for(int i = 0; i < names.Count; i++)
                {
                    if (i == 0)
                        rval += names[0].identifier;
                    else
                        rval += names[i].identifier;
                }
            }
            else
            {
                rval += "(" + arglist.identifier + ")";
            }

            rval += " | " + body.DebugString();
            return rval + ">";
        }

        public override object Clone()
        {
            if(arglist == null)
                return new SFunc(names, body, env);
            return new SFunc(arglist, body, env);
        }
    }

    class SID : SExpression {
        public string identifier;
        public SID(string id) { identifier = id; }

        public override string ToString()
        {
            return identifier;
        }

        public override string  DebugString()
        {
            return "<ID: " + identifier + ">";
        }

        public override object Clone()
        {
            return new SID(identifier);
        }
    }

    class SSymbol : SAtomic {
        public string name;
        public SSymbol(string val) { name = val; }

        public override string ToString()
        {
            return "'" + name;
        }

        public override string DebugString()
        {
            return "<Sym: " + name + ">";
        }

        public override object Clone()
        {
            return new SSymbol(name);
        }
    }

    class SInt : SAtomic {
        public int val;
        public SInt(int val) { this.val = val; }
        public override string ToString()
        {
            return val.ToString();
        }

        public override string DebugString()
        {
            return "<INT: " + val.ToString() + ">";
        }

        public override object Clone()
        {
            return new SInt(val);
        }
    }

    class SBool : SAtomic
    {
        public bool val;
        public SBool (bool v) { val = v; }
        public override string ToString()
        {
            return val ? "#t" : "#f";
        }

        public override string DebugString()
        {
            string t = val ? "TRUE" : "FALSE";
            return "<BOOL: " + t + ">";
        }

        public override object Clone()
        {
            return new SBool(val);
        }
    }

    class SList : SExpression
    {
        //Lists are really just a stack. We'll represent this as a list in which we only add and remove elements from the end of/
        //  In scheme, you maniupulate lists by pushing and popping from the head. Thus, it's hella easier add / remove from end of list
        public List<SExpression> elements;

        public SList(List<SExpression> elms) { elements = elms; elements.Reverse(); } //because head is last element, we need to invert a normal list to make it an SList
        public SList() { this.elements = new List<SExpression>(); }

        public bool isEmpty() { return elements.Count == 0; }
        public void appendElm(SExpression elm) { elements.Add(elm); }
        public SExpression head() { return elements[elements.Count - 1]; }
        public List<SExpression> body()
        {
            return elements.Take(elements.Count - 1).ToList();
        }

        public void setHead(SExpression expr) { elements[elements.Count - 1] = expr; }

        public bool isProperList()
        {
            if (isEmpty()) { return true; }
            SExpression last = elements[0];
            if(!(last is SList)) { return false; }
            return ((SList)last).isProperList();
        }

        public override string ToString()
        {
            if (isEmpty()) { return "'()"; }

            string rval = "'(";

            for(int i = elements.Count() - 1; i > 0; i--) //if it is an impoper list, you'll print a . before the last elm, so iterate to elm n-1
            {
                if(i == elements.Count() - 1)
                    rval += elements[i].ToString();
                else
                    rval += " " + elements[i].ToString();

            }

            if (!isProperList())
                rval += ". " + elements.First().ToString();

            rval += ")";

            return rval;
        }

        public override string DebugString()
        {
            if (isEmpty()) { return "<Empty List>"; }

            string rval = "<LIST: ";

            for (int i = elements.Count() - 1; i > -1; i--) //if it is an impoper list, you'll print a . before the last elm, so iterate to elm n-1
            {
                if (i == elements.Count() - 1)
                    rval += elements[i].DebugString();
                else
                    rval += " " + elements[i].DebugString();

            }

            rval += ">";

            return rval;
        }

        public override object Clone()
        {
            SList nl = new SList();
            foreach(SExpression elm in elements)
            {
                nl.appendElm((SExpression)elm.Clone());
            }
            return nl;
        }
    }

    class SNone : SExpression
    {
        public override string ToString()
        {
            return "";
        }

        public override string DebugString()
        {
            return "<NONE>";
        }

        public override object Clone()
        {
            return new SNone();
        }
    }
}
