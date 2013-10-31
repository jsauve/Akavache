using System;
using System.Net.Http;
using System.Threading;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Reactive;

namespace Akavache
{
    public static class ObservableHttpClient
    {
        /// <summary>
        /// Sends an HTTP request and attempts to cancel the request as soon as
        /// possible if requested to do so.
        /// </summary>
        /// <param name="request">The HTTP request to make</param>
        /// <param name="shouldFetchContent">If given, this predicate allows you 
        /// to cancel the request based on the returned headers. Return false to
        /// cancel reading the body</param>>
        /// <returns>A tuple of the HTTP Response and the full message 
        /// contents.</returns>
        public static IObservable<Tuple<HttpResponseMessage, byte[]>> SendAsyncObservable(this HttpClient This, HttpRequestMessage request, Func<HttpResponseMessage, bool> shouldFetchContent = null)
        {
            shouldFetchContent = shouldFetchContent ?? (_ => true);

            var cancelSignal = new AsyncSubject<Unit>();
            var ret = Observable.Create<Tuple<HttpResponseMessage, byte[]>>(async (subj, ct) => {
                try {
                    var resp = await This.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                    if (!shouldFetchContent(resp)) {
                        cancelSignal.OnNext(Unit.Default);
                        cancelSignal.OnCompleted();
                    } else {
                        var data = await resp.Content.ReadAsByteArrayAsync();

                        subj.OnNext(Tuple.Create(resp, data));
                        subj.OnCompleted();
                    }
                } catch (Exception ex) {
                    subj.OnError(ex);
                }
            });

            return ret.TakeUntil(cancelSignal).PublishLast().RefCount();
        }
    }
}