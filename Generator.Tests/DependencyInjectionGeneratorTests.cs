﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using Xunit;

using VerifyCS = Swick.DependencyInjection.Generator.IncrementalSourceGeneratorVerifier<
    Swick.DependencyInjection.Generator.DependencyInjectionGenerator>;

namespace Swick.DependencyInjection.Generator;

public class DependencyInjectionGeneratorTests
{
    private const string Attribute = """
        // <auto-generated />

        #nullable enable

        namespace Swick.DependencyInjection;

        [global::System.Diagnostics.Conditional("SWICK_DEPENDENCY_INJECTION")]
        [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
        internal sealed class RegisterAttribute : global::System.Attribute
        {
            public RegisterAttribute(global::System.Type contract, global::System.Type? service = null)
            {
            }
        }

        [global::System.Diagnostics.Conditional("SWICK_DEPENDENCY_INJECTION")]
        [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
        internal sealed class RegisterFactoryAttribute : global::System.Attribute
        {
            public RegisterFactoryAttribute(global::System.Type serviceType, global::System.String methodName)
            {
            }
        }

        [global::System.Diagnostics.Conditional("SWICK_DEPENDENCY_INJECTION")]
        [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
        internal sealed class ContainerOptionsAttribute : global::System.Attribute
        {
            public bool IsThreadSafe { get; set; }
        }

        """;

    private const string AttributeFileName = "DependencyInjectionAttributes.cs";

    [Fact]
    public async Task Empty()
    {
        var test = string.Empty;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task OnlyOneServiceCanBeRegistered()
    {
        var test = """
            using Swick.DependencyInjection;
            using System.Threading;

            namespace Test;

            public interface ITest
            {
            }

            public class TestImpl : ITest
            {
            }

            public class TestImpl2 : ITest
            {
            }

            public partial class Factory
            {
                [Register(typeof(ITest), typeof(TestImpl))]
                [{|#0:Register(typeof(ITest), typeof(TestImpl2))|}]
                private partial T Get<T>();
            }
            """;
        var created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private partial T? Get<T>()
                {
                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", created),
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError(KnownErrors.DuplicateService).WithLocation(0),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task FactoryWithIncorrectReturn()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public interface ITest
            {
            }

            public interface ITest2
            {
            }

            public partial class Factory
            {
                [{|#0:RegisterFactory(typeof(ITest), nameof(SomeFactory))|}]
                private partial T Get<T>();

                private ITest2 SomeFactory() => default;
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private partial T? Get<T>()
                {
                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError(KnownErrors.InvalidFactory).WithLocation(0),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task FactoryWithParameters()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public interface ITest
            {
            }

            public class TestImpl : ITest
            {
            }

            public partial class Factory
            {
                [{|#0:RegisterFactory(typeof(ITest), nameof(SomeFactory))|}]
                private partial T Get<T>();

                private ITest SomeFactory(int i) => default;
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private partial T? Get<T>()
                {
                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError(KnownErrors.InvalidFactory).WithLocation(0),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task NonexistentFactory()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public interface ITest
            {
            }

            public partial class Factory
            {
                [{|#0:RegisterFactory(typeof(ITest), "SomeFactory")|}]
                private partial T Get<T>();
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private partial T? Get<T>()
                {
                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError(KnownErrors.InvalidFactory).WithLocation(0),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateKnown()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public interface ITest
            {
            }

            public class TestImpl : ITest
            {
            }

            public partial class Factory
            {
                [Register(typeof(ITest), typeof(TestImpl))]
                [{|#0:Register(typeof(ITest), typeof(TestImpl))|}]
                private partial T Get<T>();
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private partial T? Get<T>()
                {
                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError(KnownErrors.DuplicateService).WithLocation(0),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task ConcreteOnly()
    {
        var test = """
            using Swick.DependencyInjection;

            namespace Test;

            public class TestImpl
            {
            }

            public partial class Factory
            {
                [Register(typeof(TestImpl))]
                private partial T Get<T>();
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private global::Test.TestImpl? _TestImpl;

                private partial T? Get<T>()
                {
                    if (typeof(T) == typeof(global::Test.TestImpl))
                    {
                        if (_TestImpl is null)
                        {
                            _TestImpl = new global::Test.TestImpl();
                        }

                        return (T)(object)_TestImpl;
                    }

                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task ConcreteOnlyThreadSafe()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public class TestImpl
            {
            }

            public partial class Factory
            {
                [Register(typeof(TestImpl))]
                [ContainerOptions(IsThreadSafe = true)]
                private partial T Get<T>();
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            using System.Threading;

            namespace Test;

            public partial class Factory
            {
                private global::Test.TestImpl? _TestImpl;

                private partial T? Get<T>()
                {
                    if (typeof(T) == typeof(global::Test.TestImpl))
                    {
                        if (_TestImpl is null)
                        {
                            Interlocked.CompareExchange(ref _TestImpl, new global::Test.TestImpl(), null);
                        }

                        return (T)(object)_TestImpl;
                    }

                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorNestedClass()
    {
        var test = @"using Swick.DependencyInjection;

namespace Test;

public interface ITest
{
}

public class TestImpl : ITest
{
}

public partial class Factory
{
    private partial class Other
    {
        [Register(typeof(ITest), typeof(TestImpl))]
        [ContainerOptions(IsThreadSafe = true)]
        private partial T Get<T>();
    }
}";
        const string Created = """
            // <auto-generated/>

            #nullable enable

            using System.Threading;

            namespace Test;

            public partial class Factory
            {
                private partial class Other
                {
                    private global::Test.ITest? _TestImpl;

                    private partial T? Get<T>()
                    {
                        if (typeof(T) == typeof(global::Test.ITest))
                        {
                            if (_TestImpl is null)
                            {
                                Interlocked.CompareExchange(ref _TestImpl, new global::Test.TestImpl(), null);
                            }

                            return (T)_TestImpl;
                        }

                        return default;
                    }
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Other.Get.cs", Created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGenerator()
    {
        var test = """
            using Swick.DependencyInjection;

            using System.Threading;

            namespace Test;

            public interface ITest
            {
            }

            public class TestImpl : ITest
            {
            }

            public partial class Factory
            {
                [Register(typeof(ITest), typeof(TestImpl))]
                [ContainerOptions(IsThreadSafe = true)]
                private partial T Get<T>();
            }
            """;
        var created = """
            // <auto-generated/>

            #nullable enable

            using System.Threading;

            namespace Test;

            public partial class Factory
            {
                private global::Test.ITest? _TestImpl;

                private partial T? Get<T>()
                {
                    if (typeof(T) == typeof(global::Test.ITest))
                    {
                        if (_TestImpl is null)
                        {
                            Interlocked.CompareExchange(ref _TestImpl, new global::Test.TestImpl(), null);
                        }

                        return (T)_TestImpl;
                    }

                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorMultipleServices()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public interface ITest1
            {
            }

            public class TestImpl1 : ITest1
            {
            }

            public interface ITest2
            {
            }

            public class TestImpl2 : ITest2
            {
            }

            public partial class Factory
            {
                [Register(typeof(ITest1), typeof(TestImpl1))]
                [Register(typeof(ITest2), typeof(TestImpl2))]
                [ContainerOptions(IsThreadSafe = true)]
                private partial T Get<T>();
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            using System.Threading;

            namespace Test;

            public partial class Factory
            {
                private global::Test.ITest1? _TestImpl1;
                private global::Test.ITest2? _TestImpl2;

                private partial T? Get<T>()
                {
                    if (typeof(T) == typeof(global::Test.ITest1))
                    {
                        if (_TestImpl1 is null)
                        {
                            Interlocked.CompareExchange(ref _TestImpl1, new global::Test.TestImpl1(), null);
                        }

                        return (T)_TestImpl1;
                    }

                    if (typeof(T) == typeof(global::Test.ITest2))
                    {
                        if (_TestImpl2 is null)
                        {
                            Interlocked.CompareExchange(ref _TestImpl2, new global::Test.TestImpl2(), null);
                        }

                        return (T)_TestImpl2;
                    }

                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorMultipleServicesNotThreadSafe()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public interface ITest1
            {
            }

            public class TestImpl1 : ITest1
            {
            }

            public interface ITest2
            {
            }

            public class TestImpl2 : ITest2
            {
            }

            public partial class Factory
            {
                [Register(typeof(ITest1), typeof(TestImpl1))]
                [Register(typeof(ITest2), typeof(TestImpl2))]
                private partial T Get<T>();
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private global::Test.ITest1? _TestImpl1;
                private global::Test.ITest2? _TestImpl2;

                private partial T? Get<T>()
                {
                    if (typeof(T) == typeof(global::Test.ITest1))
                    {
                        if (_TestImpl1 is null)
                        {
                            _TestImpl1 = new global::Test.TestImpl1();
                        }

                        return (T)_TestImpl1;
                    }

                    if (typeof(T) == typeof(global::Test.ITest2))
                    {
                        if (_TestImpl2 is null)
                        {
                            _TestImpl2 = new global::Test.TestImpl2();
                        }

                        return (T)_TestImpl2;
                    }

                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorFactoryMethod()
    {
        const string Test = """
            using Swick.DependencyInjection;

            namespace Test;

            public interface ITest1
            {
            }

            public class TestImpl1 : ITest1
            {
            }

            public interface ITest2
            {
            }

            public class TestImpl2 : ITest2
            {
            }

            public partial class Factory
            {
                [Register(typeof(ITest1), typeof(TestImpl1))]
                [RegisterFactory(typeof(ITest2), nameof(CreateTest2))]
                private partial T Get<T>();

                private ITest2 CreateTest2() => new TestImpl2();
            }
            """;
        const string Created = """
            // <auto-generated/>

            #nullable enable

            namespace Test;

            public partial class Factory
            {
                private global::Test.ITest1? _TestImpl1;
                private global::Test.ITest2? _ITest2;

                private partial T? Get<T>()
                {
                    if (typeof(T) == typeof(global::Test.ITest1))
                    {
                        if (_TestImpl1 is null)
                        {
                            _TestImpl1 = new global::Test.TestImpl1();
                        }

                        return (T)_TestImpl1;
                    }

                    if (typeof(T) == typeof(global::Test.ITest2))
                    {
                        if (_ITest2 is null)
                        {
                            _ITest2 = CreateTest2();
                        }

                        return (T)_ITest2;
                    }

                    return default;
                }
            }

            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { Test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), AttributeFileName, Attribute),
                    (typeof(DependencyInjectionGenerator), "Test.Factory.Get.cs", Created),
                },
            },
        }.RunAsync();
    }
}
