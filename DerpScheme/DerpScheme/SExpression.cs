using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    delegate SExpression Func(List<SExpression> args, Environment e);

    abstract class SExpression
    {
        public Token originalToken;
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
    }

    class SFunc : SApplicable //SFuncs are lambda functions
    {
        private Environment env; //private environment for sweet, sweet closures
        private List<SID> names; //names bound in definition
        private SExpression body; //body of execution

        public SFunc(List<SID> names, SList body, Environment e)
        {
            //our starting environment is the env we were defined in
            env = new Environment(e);
            foreach(SID id in names)
            {
                env.addVal(id.identifier, new SNone()); //add bound variables to environment with default None value
            }
            this.names = names;
            this.body = body;
        }

        override public SExpression apply(List<SExpression> args, Environment e)
        {
            if (args.Count != names.Count)
                throw new Exception("Incorrect number of args");
            for(int i = 0; i < args.Count; i++)
            {
                //evaluate all of the args and bind them to my local env according to their cooresponding values
                env.setLocalVal(names[i].identifier, DerpScheme.DerpInterpreter.evaluate(args[i], e));
            }
            return DerpScheme.DerpInterpreter.evaluate(body, env); //execute lambda and return
        }

        public override string ToString() { return "#procedure"; } 
    }

    class SID : SExpression {
        public string identifier;
        public SID(string id) { identifier = id; }

        public override string ToString()
        {
            return "<ID: " + identifier + ">";
        }
    }

    class SSymbol : SAtomic {
        public string name;
        public SSymbol(string val) { name = val; }

        public override string ToString()
        {
            return "<Sym: " + name + ">";
        }
    }

    class SInt : SAtomic {
        public int val;
        public SInt(int val) { this.val = val; }
        public override string ToString()
        {
            return val.ToString();
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
            return ((SList)(last)).isEmpty();
        }

        public override string ToString()
        {
            if (isEmpty()) { return "<Empty List>"; }

            string rval = "(";

            for(int i = elements.Count() - 1; i > 0; i--) //if it is an impoper list, you'll print a . before the last elm, so iterate to elm n-1
            {
                rval += elements[i].ToString() + " ";
            }

            if (!isProperList())
                rval += ". ";

            rval += elements.First().ToString() + ")";

            return rval;
        }

    }

    class SNone : SExpression
    {
        public override string ToString()
        {
            return "<NONE>";
        }
    }
}
