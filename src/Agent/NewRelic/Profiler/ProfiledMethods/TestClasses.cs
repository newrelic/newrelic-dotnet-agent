// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NewRelic.Agent.Tests.ProfiledMethods
{

    public class EmptyClass
    {
    }

    public class SimpleClass
    {
        public int data = 0;
    }

    public class GenericClass<GenericType>
    {
        GenericType savedData;
        private object[] someOtherData;

        public GenericClass()
        {
        }

        public GenericClass(GenericType inputData)
        {
            savedData = inputData;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void DefaultMethod()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void DefaultMethod(GenericType argumentData)
        {
            savedData = argumentData;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void GenericMethod<TY>(TY aGenericParameter)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void GenericMethodTwo<TY>(TY aGenericParameter, GenericType anotherGenericParameter)
        {
            this.someOtherData = new object[2];
            this.someOtherData[0] = aGenericParameter;
            this.someOtherData[1] = anotherGenericParameter;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public TY GenericMethodThree<TY>(TY aGenericParameter)
        {
            return aGenericParameter;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void GenericMethodFour<TY>(TY aGenericParameter, params TY[] list)
        {
            var count = 1;
            if (list != null)
            {
                count += list.Length;
            }

            this.someOtherData = new object[count];
            this.someOtherData[0] = aGenericParameter;
            var n = 1;
            foreach (var item in list)
            {
                this.someOtherData[n++] = item;
            }
        }
    }

    public class GenericClass2<GenericType>
    {
        GenericType savedData;

        public GenericClass2(GenericType inputData)
        {
            savedData = inputData;
        }
    }

    public static class StaticGenericClass<GenericType>
    {
        private static GenericType savedData;
        private static object[] someOtherData;

        static StaticGenericClass()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public static void DefaultMethod()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public static void DefaultMethod(GenericType argumentData)
        {
            savedData = argumentData;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public static void GenericMethod<TY>(TY aGenericParameter)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public static void GenericMethodTwo<TY>(TY aGenericParameter, GenericType anotherGenericParameter)
        {
            someOtherData = new object[2];
            someOtherData[0] = aGenericParameter;
            someOtherData[1] = anotherGenericParameter;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public static TY GenericMethodThree<TY>(TY aGenericParameter)
        {
            return aGenericParameter;
        }
    }

    public class GenericClass3<GenericType> : IEnumerable<GenericType>
    {
        private List<GenericType> _list = null;

        public GenericClass3()
        {
            CreateList();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public int GetCount()
        {
            if (_list == null)
                return 0;

            return _list.Count;
        }

        public IEnumerator<GenericType> GetEnumerator()
        {
            if (_list == null)
            {
                CreateList();
            }
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerator<GenericType> enumerator = GetEnumerator();
            return enumerator;
        }

        private void CreateList()
        {
            this._list = new List<GenericType>();
        }
    }

    public class GenericClassValueTypeConstraint<TGenericType> where TGenericType : struct
    {
        private TGenericType _someValue;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void DefaultMethod(TGenericType genericParameter)
        {
            this._someValue = genericParameter;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void GenericMethodOne<TGenericTypeThree>(TGenericTypeThree genericParameter) where TGenericTypeThree : struct
        {
            TGenericTypeThree temp = genericParameter;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public bool GenericMethodTwo<TGenericTypeThree>(TGenericTypeThree genericParameterOne, TGenericType genericParameterTwo)
        {
            bool firstArgIsNullOrValueType = genericParameterOne == null;
            this._someValue = genericParameterTwo;
            return firstArgIsNullOrValueType;
        }
    }

    public class GenericClassMultipleValueTypeConstraints<TGenericTypeOne, TGenericTypeTwo>
        where TGenericTypeOne : struct
        where TGenericTypeTwo : struct
    {
        private TGenericTypeOne _someValue;
        private TGenericTypeTwo _otherValue;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void DefaultMethod(TGenericTypeOne genericParameterOne, TGenericTypeTwo genericParameterTwo)
        {
            this._someValue = genericParameterOne;
            this._otherValue = genericParameterTwo;
        }
    }

    public class GenericClassMultipleTypeConstraints<TGenericTypeOne, TGenericTypeTwo>
        where TGenericTypeOne : struct
        where TGenericTypeTwo : class
    {
        private TGenericTypeOne _aValue;
        private TGenericTypeTwo _anObject;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void DefaultMethod(TGenericTypeOne valueGenericParameter, TGenericTypeTwo objGenericParameter)
        {
            this._aValue = valueGenericParameter;
            this._anObject = objGenericParameter;
        }
    }

    public class GenericClassClassConstraint<TGenericType> where TGenericType : class
    {
        private TGenericType _someObject;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void GenericMethod<TGenericTypeTwo>(TGenericTypeTwo genericParameter) where TGenericTypeTwo : TGenericType
        {
            this._someObject = genericParameter;
        }
    }

    public class GenericClassComplexConstraints<TGenericTypeOne, TGenericTypeTwo, TGenericTypeThree>
        where TGenericTypeOne : TGenericTypeThree
        where TGenericTypeTwo : new()
    {
        private TGenericTypeThree _someObject;
        private TGenericTypeTwo _someOtherObject;
        private BaseClass _someBaseObject;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void DefaultMethod(TGenericTypeOne genericParameterOne, TGenericTypeTwo genericParameterTwo)
        {
            this._someObject = genericParameterOne;
            this._someOtherObject = genericParameterTwo;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void GenericMethodOne<TGenericTypeFour>(TGenericTypeFour genericParameter, BaseClass defaultParameter) where TGenericTypeFour : TGenericTypeTwo
        {
            this._someOtherObject = genericParameter;
            this._someBaseObject = defaultParameter;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public TGenericTypeFive GenericMethodTwo<TGenericTypeFive, TGenericTypeSix>(TGenericTypeFive genericParameterOne, TGenericTypeSix genericParameterTwo, TGenericTypeThree genericParameterThree)
            where TGenericTypeSix : class
            where TGenericTypeFive : class
        {
            if (genericParameterTwo != null && this._someObject != null)
            {
                if (genericParameterTwo.GetType() == this._someObject.GetType())
                {
                    return null;
                }
            }

            return genericParameterOne;
        }
    }

    #region Covariance and Contravariance Test Interfaces and Classes
    public interface ICovariantFactory<out TGenericType>
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        TGenericType CreateInstance();
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        TGenericType GenericMethod<TGenericTypeTwo>(TGenericTypeTwo aGenericParameter);
    }

    public class CovariantFactory<T> : ICovariantFactory<T> where T : new()
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public T CreateInstance()
        {
            return new T();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public T GenericMethod<TGenericTypeTwo>(TGenericTypeTwo aGenericParameter)
        {
            var x = 0;
            Object obj = aGenericParameter;
            if (obj == null)
            {
                x = (x == 0) ? 1 : 3;
            }
            else
            {
                x = 2;
            }
            return new T();
        }
    }

    interface IAnimal
    {
        String Speak();
    }

    class Whale : IAnimal
    {
        public String Speak()
        {
            return "I'm a whale.";
        }
    }

    class Giraffe : IAnimal
    {
        public String Speak()
        {
            return "I'm a Giraffe";
        }
    }

    interface IContravariantProcessor<in T>
    {
        void Process(IEnumerable<T> ts);
        void GenericMethod<TY>(TY aGenericParameter);
    }

    class ContravariantProcessor<T> : IContravariantProcessor<T> where T : IAnimal
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void Process(IEnumerable<T> ts)
        {
            foreach (var t in ts)
            {
                t.Speak();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public void GenericMethod<TY>(TY aGenericParameter)
        {
            var x = 0;
            Object obj = aGenericParameter;
            if (obj == null)
            {
                x = (x == 0) ? 1 : 3;
            }
            else
            {
                x = 2;
            }
        }
    }
    #endregion

    public class DefaultClass
    {
        public int X;

        public void DefaultMethod(int myargument)
        {
            X = myargument;
        }
    }

    public interface IInterface
    {
        int ReturnNumber(int parameter);
    }

    public class Implementation : IInterface
    {
        int IInterface.ReturnNumber(int parameter)
        {
            return parameter;
        }
    }

    public interface IExecutionStepSimulator
    {
        void DoFirstTry1();
        void DoSecondTry1();
        void DoThirdTry();
        void DoFinally();
        void DoSecondTry2();
        void DoFirstCatch();
        void DoSecondCatch();
        void DoFirstTry2();
    }
    public class ExecutionStepSimulator : IExecutionStepSimulator
    {
        public Action doFirstTry1 = null;
        public bool firstTry1Hit = false;
        public void DoFirstTry1()
        {
            firstTry1Hit = true;
            if (doFirstTry1 != null) doFirstTry1();
        }

        public Action doFirstTry2 = null;
        public bool firstTry2Hit = false;
        public void DoFirstTry2()
        {
            firstTry2Hit = true;
            if (doFirstTry2 != null) doFirstTry2();
        }

        public Action doSecondTry1 = null;
        public bool secondTry1Hit = false;
        public void DoSecondTry1()
        {
            secondTry1Hit = true;
            if (doSecondTry1 != null) doSecondTry1();
        }

        public Action doSecondTry2 = null;
        public bool secondTry2Hit = false;
        public void DoSecondTry2()
        {
            secondTry2Hit = true;
            if (doSecondTry2 != null) doSecondTry2();
        }

        public Action doThirdTry = null;
        public bool thirdTryHit = false;
        public void DoThirdTry()
        {
            thirdTryHit = true;
            if (doThirdTry != null) doThirdTry();
        }

        public Action doFinally = null;
        public bool finallyHit = false;
        public void DoFinally()
        {
            finallyHit = true;
            if (doFinally != null) doFinally();
        }

        public Action doFirstCatch = null;
        public bool firstCatchHit = false;
        public void DoFirstCatch()
        {
            firstCatchHit = true;
            if (doFirstCatch != null) doFirstCatch();
        }

        public Action doSecondCatch = null;
        public bool secondCatchHit = false;
        public void DoSecondCatch()
        {
            secondCatchHit = true;
            if (doSecondCatch != null) doSecondCatch();
        }
    }

    public class OuterClass
    {
        public class InnerClass
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
            public void InnerClassMethod() { }
        }
    }

    public class BaseClass
    {
        public int i = 0;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public virtual void Foo()
        {
            ++i;
        }
    }

    public class DerivedClass : BaseClass
    {
        public int j = 0;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
        public override void Foo()
        {
            ++j;
            base.Foo();
        }
    }

    public class MyException : Exception
    {
    }

    public class OuterGenericClass<TGenericType>
    {
        public class InnerDefaultClass
        {
            private object[] someData;

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
            public void InnerClassMethod() { }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
            public void InnerClassMethodWithClassGenericParam(TGenericType typeGenericParameter)
            {
                someData = new object[1];
                someData[0] = typeGenericParameter;
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
            public TGenericType InnerClassMethodReturnsIt(TGenericType typeGenericParameter)
            {
                return typeGenericParameter;
            }


            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
            public void InnerGenericMethod<TAnotherGenericType>(TAnotherGenericType genericParameter)
            {
                someData = new object[1];
                someData[0] = genericParameter;
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
            public TAnotherGenericType InnerGenericMethodReturnsIt<TAnotherGenericType>(TAnotherGenericType genericParameter)
            {
                return genericParameter;
            }
        }
    }

}
