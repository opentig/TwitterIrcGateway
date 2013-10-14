using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// 遅延実行の処理を提供します。
    /// </summary>
    public static class Deferred
    {
        /// <summary>
        /// 遅延実行の状態を表します。
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        public class DeferredState<TResult>
        {
            public Boolean IsCanceled { get; private set; }
            public Boolean IsRunning { get; internal set; }
            public IAsyncResult AsyncResult { get; internal set; }
            public Func<TResult> Target { get; internal set; }
            public ManualResetEvent WaitHandle { get; set; }
            
            /// <summary>
            /// 処理をキャンセルします。
            /// </summary>
            /// <returns>キャンセル出来た場合にはtrue、既に実行が完了されたなどの理由でキャンセル出来なかった場合にはfalse</returns>
            public Boolean Cancel()
            {
                IsCanceled = true;
                if (AsyncResult.IsCompleted)
                {
                    return false;
                }

                DeferredState<TResult> state = AsyncResult.AsyncState as DeferredState<TResult>;
                state.WaitHandle.Set();
                
                Target.EndInvoke(AsyncResult);
                return IsRunning;
            }
            
            /// <summary>
            /// 遅延実行結果を取得します。処理が実行されていない場合には完了を待ちます。
            /// </summary>
            /// <returns></returns>
            public TResult Result()
            {
                return Target.EndInvoke(AsyncResult);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="asyncCallback"></param>
        /// <returns></returns>
        private static DeferredState<TResult> DeferredInvokeInternal<TResult>(Func<TResult> func, Int32 millisecond, AsyncCallback asyncCallback)
        {
            DeferredState<TResult> state = new DeferredState<TResult>();
            state.WaitHandle = new ManualResetEvent(false);

            // 暫定で0から10秒
            if (millisecond > 10 * 1000)
                millisecond = 10 * 1000;
            else if (millisecond < 0)
                millisecond = 0;

            Func<TResult> d = () =>
            {
                state.WaitHandle.WaitOne(millisecond);

                lock (state)
                {
                    if (state.IsCanceled)
                    {
                        state.IsRunning = true;
                        return default(TResult);
                    }
                    else
                    {
                        return func();
                    }
                }
            };
            state.Target = d;
            state.AsyncResult = d.BeginInvoke(asyncCallback, state);
            return state;
        }
        /// <summary>
        /// メソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<TResult>(this Func<TResult> func, Int32 millisecond)
        {
            return DeferredInvoke(func, millisecond, null);
        }
        /// <summary>
        /// コールバックを指定してメソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="asyncCallback"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<TResult>(this Func<TResult> func, Int32 millisecond, AsyncCallback asyncCallback)
        {
            return DeferredInvokeInternal(func, millisecond, asyncCallback);
        }
        /// <summary>
        /// メソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="arg1"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T, TResult>(this Func<T, TResult> func, Int32 millisecond, T arg1)
        {
            return DeferredInvoke(func, millisecond, null, arg1);
        }
        /// <summary>
        /// コールバックを指定してメソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="asyncCallback"></param>
        /// <param name="arg1"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T, TResult>(this Func<T, TResult> func, Int32 millisecond, AsyncCallback asyncCallback, T arg1)
        {
            return DeferredInvokeInternal(() => func(arg1), millisecond, asyncCallback);
        }
        /// <summary>
        /// メソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T1, T2, TResult>(this Func<T1, T2, TResult> func, Int32 millisecond, T1 arg1, T2 arg2)
        {
            return DeferredInvoke(func, millisecond, null, arg1, arg2);
        }
        /// <summary>
        /// コールバックを指定してメソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="asyncCallback"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T1, T2, TResult>(this Func<T1, T2, TResult> func, Int32 millisecond, AsyncCallback asyncCallback, T1 arg1, T2 arg2)
        {
            return DeferredInvokeInternal(() => func(arg1, arg2), millisecond, asyncCallback);
        }
        /// <summary>
        /// メソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> func, Int32 millisecond, T1 arg1, T2 arg2, T3 arg3)
        {
            return DeferredInvoke(func, millisecond, null, arg1, arg2, arg3);
        }
        /// <summary>
        /// コールバックを指定してメソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="asyncCallback"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> func, Int32 millisecond, AsyncCallback asyncCallback, T1 arg1, T2 arg2, T3 arg3)
        {
            return DeferredInvokeInternal(() => func(arg1, arg2, arg3), millisecond, asyncCallback);
        }
        /// <summary>
        /// メソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="arg4"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> func, Int32 millisecond, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            return DeferredInvoke(func, millisecond, null, arg1, arg2, arg3, arg4);
        }
        /// <summary>
        /// コールバックを指定してメソッドを遅延実行します。
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <param name="millisecond"></param>
        /// <param name="asyncCallback"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="arg4"></param>
        /// <returns></returns>
        public static DeferredState<TResult> DeferredInvoke<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, TResult> func, Int32 millisecond, AsyncCallback asyncCallback, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            return DeferredInvokeInternal(() => func(arg1, arg2, arg3, arg4), millisecond, asyncCallback);
        }
    }
}
