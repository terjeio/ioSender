/*
 * 
 * Pulled from https://raw.githubusercontent.com/brentedwards/MvvmFabric/master/MvvmFabric/ActionCommand.cs
 * 
 * Code simplification by Terje Io
 * 
 */

using System;
using System.Windows.Input;

namespace CNC.Core
{
    /// <summary>
    /// An ActionCommand is an ICommand which executes an Action with a specific parameter type.
    /// </summary>
    /// <typeparam name="TParameter">The type of parameter which the Action takes.</typeparam>
    public sealed class ActionCommand<TParameter> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private System.Action<TParameter> ExecuteMethod { get; set; }
        private Func<TParameter, bool> CanExecuteMethod { get; set; }

        /// <summary>
        /// Constructor for ActionCommand.
        /// </summary>
        /// <param name="executeMethod">The Action to be executed.</param>
        public ActionCommand(System.Action<TParameter> executeMethod)
        {
            ExecuteMethod = executeMethod;
        }

        /// <summary>
        /// Constructor for ActionCommand.
        /// </summary>
        /// <param name="executeMethod">The Action to be executed.</param>
        /// <param name="canExecuteMethod">
        /// The optional Func to be called when determining if the command can be executed.
        /// </param>
        public ActionCommand(System.Action<TParameter> executeMethod, Func<TParameter, bool> canExecuteMethod) : this(executeMethod)
        {
            CanExecuteMethod = canExecuteMethod;
        }

        public bool CanExecute(TParameter parameter)
        {
            var canExecute = true;
            if (CanExecuteMethod != null)
            {
                canExecute = CanExecuteMethod(parameter);
            }

            return canExecute;
        }

        bool ICommand.CanExecute(object parameter)
        {
            var canExecute = false;
            if (parameter is TParameter)
            {
                canExecute = CanExecute((TParameter)parameter);
            }
            else if (parameter == null)
            {
                canExecute = CanExecute(default(TParameter));
            }

            return canExecute;
        }

        public void Execute(TParameter parameter)
        {
            ExecuteMethod?.Invoke(parameter);
        }

        void ICommand.Execute(object parameter)
        {
            Execute((TParameter)parameter);
        }

        public void NotifyCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, new EventArgs());
        }
    }

    /// <summary>
    /// An ActionCommand is an ICommand which executes an Action.
    /// </summary>
    public sealed class ActionCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private System.Action ExecuteMethod { get; set; }
        private Func<bool> CanExecuteMethod { get; set; }

        /// <summary>
        /// Constructor for ActionCommand.
        /// </summary>
        /// <param name="executeMethod">The Action to be executed.</param>
        public ActionCommand(System.Action executeMethod)
        {
            ExecuteMethod = executeMethod;
        }

        /// <summary>
        /// Constructor for ActionCommand.
        /// </summary>
        /// <param name="executeMethod">The Action to be executed.</param>
        /// <param name="canExecuteMethod">
        /// The optional Func to be called when determining if the command can be executed.
        /// </param>
        public ActionCommand(System.Action executeMethod, Func<bool> canExecuteMethod) : this(executeMethod)
        {
            CanExecuteMethod = canExecuteMethod;
        }

        public bool CanExecute(object parameter)
        {
            var canExecute = true;
            if (CanExecuteMethod != null)
            {
                canExecute = CanExecuteMethod();
            }

            return canExecute;
        }

        public void Execute(object parameter)
        {
            ExecuteMethod?.Invoke();
        }

        public void NotifyCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, new EventArgs());
        }
    }
}
