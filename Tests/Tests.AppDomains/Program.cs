////////////////////////////////////////////////////////////////////////////
//
// Copyright 2021 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Realms;

namespace Tests.AppDomains
{
    public static class Program
    {
        private static string _type;

        public static void Main(string[] args)
        {
            if (AppDomain.CurrentDomain.FriendlyName == $"{Assembly.GetExecutingAssembly().GetName().Name}.exe")
            {
                RunMain();
            }
            else
            {
                RunChild(args?.ElementAtOrDefault(0));
            }
        }

        private static void RunMain()
        {
            _type = "Main";

            AsyncContext.Run(async () =>
            {
                await LoadChild();

                await LoadChild();

                await Task.Delay(2000);

                Log("Done");
            });
        }

        private static async Task LoadChild()
        {
            var domain = AppDomain.CreateDomain("Test Child Domain");

            _ = Task.Run(() =>
            {
                domain.ExecuteAssemblyByName(Assembly.GetExecutingAssembly().GetName(), "foo.realm");
            });

            await Task.Delay(2000);

            Log("Trying to unload the domain.");

            var sw = new Stopwatch();
            sw.Start();

            AppDomain.Unload(domain);

            sw.Stop();

            Log($"Unloaded child domain in {sw.ElapsedMilliseconds} ms");
        }

        private static Realm _realm;
        private static StringObject _stringObject;

        private static void RunChild(string realmPath)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                Log("Resolve: " + e.Name);

                return null;
            };

            _type = "Child";

            if (string.IsNullOrEmpty(realmPath))
            {
                throw new NotSupportedException("Expected a valid Realm path in child domain.");
            }

            try
            {
                AsyncContext.Run(async () =>
                {
                    Log($"Current domain is: {AppDomain.CurrentDomain.FriendlyName}");
                    Log($"Opening Realm at path {realmPath}");

                    _realm = Realm.GetInstance(realmPath);

                    _realm.All<StringObject>().AsRealmCollection().CollectionChanged += (s, e) =>
                    {
                        Log("Collection changed");
                    };

                    _stringObject = _realm.Write(() =>
                    {
                        return _realm.Add(new StringObject
                        {
                            Value = Guid.NewGuid().ToString()
                        });
                    });

                    _stringObject.PropertyChanged += (s, e) =>
                    {
                        Log("Property changed");
                    };

                    Log($"Objects in Realm: {_realm.All<StringObject>().Count()}");

                    while (true)
                    {
                        await Task.Delay(1000);
                        Log($"PropertyValue: {_stringObject.Value}");
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        private static void Log(string message)
        {
            Console.WriteLine($"{_type}: {message}");
        }
    }
}
