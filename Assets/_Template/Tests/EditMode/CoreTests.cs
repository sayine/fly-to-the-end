using System;
using NUnit.Framework;
using Template.Core;

namespace Template.Tests
{
    public class ServiceLocatorTests
    {
        private interface IFoo { }
        private class Foo : IFoo, IDisposable
        {
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        [SetUp, TearDown]
        public void Reset() => ServiceLocator.Clear();

        [Test]
        public void RegisterAndGet_ByInterface()
        {
            var foo = new Foo();
            ServiceLocator.Register<IFoo>(foo);
            Assert.AreSame(foo, ServiceLocator.Get<IFoo>());
        }

        [Test]
        public void Get_Unregistered_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IFoo>());
            Assert.IsFalse(ServiceLocator.TryGet<IFoo>(out _));
        }

        [Test]
        public void Clear_DisposesServices()
        {
            var foo = new Foo();
            ServiceLocator.Register<IFoo>(foo);
            ServiceLocator.Clear();
            Assert.IsTrue(foo.Disposed);
            Assert.IsFalse(ServiceLocator.IsRegistered<IFoo>());
        }
    }

    public class StateMachineTests
    {
        private class Recorder : State
        {
            public int Enters, Exits;
            public Action OnEnter;
            public override void Enter() { Enters++; OnEnter?.Invoke(); }
            public override void Exit() => Exits++;
        }

        [Test]
        public void ChangeState_CallsExitThenEnter()
        {
            var sm = new StateMachine();
            var a = new Recorder();
            var b = new Recorder();
            sm.ChangeState(a);
            sm.ChangeState(b);
            Assert.AreEqual(1, a.Enters);
            Assert.AreEqual(1, a.Exits);
            Assert.AreEqual(1, b.Enters);
            Assert.AreSame(b, sm.Current);
            Assert.AreSame(a, sm.Previous);
        }

        [Test]
        public void ChangeState_FromInsideEnter_IsQueued()
        {
            var sm = new StateMachine();
            var a = new Recorder();
            var b = new Recorder();
            a.OnEnter = () => sm.ChangeState(b);
            sm.ChangeState(a);
            Assert.AreSame(b, sm.Current);
            Assert.AreEqual(1, a.Exits);
        }
    }
}
