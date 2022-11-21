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
            public RegisterFactoryAttribute(global::System.Type contract, global::System.Type? service = null)
            {
            }

            public bool IsExternal { get; set; }
        }

        [global::System.Diagnostics.Conditional("SWICK_DEPENDENCY_INJECTION")]
        [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
        internal sealed class ContainerOptionsAttribute : global::System.Attribute
        {
            public bool IsThreadSafe { get; set; }
        }

        """;

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
                    (typeof(DependencyInjectionGenerator), "DependencyInjectionAttributes.cs", Attribute),
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
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "DependencyInjectionAttributes.cs", Attribute),
                },
                ExpectedDiagnostics =
                {
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateKnown()
    {
        var test = @"using DocumentFormat.OpenXml;

namespace Test;

public interface ITest
{
}

public class TestImpl : ITest
{
}

public partial class Factory
{
    [KnownFeature(typeof(ITest), typeof(TestImpl))]
    [{|#0:KnownFeature(typeof(ITest), typeof(TestImpl))|}]
    [ThreadSafe]
    private partial T Get<T>();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError("OOX1000").WithLocation(0),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task ConcreteOnly()
    {
        var test = @"using DocumentFormat.OpenXml;

namespace Test;

public class TestImpl
{
}

public partial class Factory
{
    [KnownFeature(typeof(TestImpl))]
    private partial T Get<T>();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task ConcreteOnlyThreadSafe()
    {
        var test = @"using DocumentFormat.OpenXml;

namespace Test;

public class TestImpl
{
}

public partial class Factory
{
    [KnownFeature(typeof(TestImpl))]
    [ThreadSafe]
    private partial T Get<T>();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorNestedClass()
    {
        var test = @"using DocumentFormat.OpenXml;

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
        [KnownFeature(typeof(ITest), typeof(TestImpl))]
        [ThreadSafe]
        private partial T Get<T>();
    }
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Other_Get.cs", created),
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
                [ThreadSafe]
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
                    (typeof(DependencyInjectionGenerator), "DependencyInjectionAttributes.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorMultipleServices()
    {
        var test = @"using DocumentFormat.OpenXml;

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
    [KnownFeature(typeof(ITest1), typeof(TestImpl1))]
    [KnownFeature(typeof(ITest2), typeof(TestImpl2))]
    [ThreadSafe]
    private partial T Get<T>();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorMultipleServicesNotThreadSafe()
    {
        var test = @"using DocumentFormat.OpenXml;

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
    [KnownFeature(typeof(ITest1), typeof(TestImpl1))]
    [KnownFeature(typeof(ITest2), typeof(TestImpl2))]
    private partial T Get<T>();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task SingleGeneratorFactoryMethod()
    {
        var test = @"using DocumentFormat.OpenXml;

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
    [KnownFeature(typeof(ITest1), typeof(TestImpl1))]
    [KnownFeature(typeof(ITest2), Factory = nameof(CreateTest2))]
    private partial T Get<T>();

    private ITest2 CreateTest2() => new TestImpl2();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Test;

public partial class Factory
{
    private global::Test.ITest1? _TestImpl1;
    private global::Test.ITest2? _CreateTest2;

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
            if (_CreateTest2 is null)
            {
                _CreateTest2 = CreateTest2();
            }

            return (T)_CreateTest2;
        }

        return default;
    }
}
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task DelegatedFeaturesMethod()
    {
        var test = @"using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Features;

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
    [KnownFeature(typeof(ITest1), typeof(TestImpl1))]
    [DelegatedFeature(nameof(OtherFeatures))]
    private partial T Get<T>();

    private ITest2 CreateTest2() => new TestImpl2();

    private IFeatureCollection OtherFeatures() => null;
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Test;

public partial class Factory
{
    private global::Test.ITest1? _TestImpl1;

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

        if (OtherFeatures() is global::DocumentFormat.OpenXml.Features.IFeatureCollection other1 && other1.Get<T>() is T result1)
        {
            return result1;
        }

        return default;
    }
}
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task DelegatedFeaturesProperty()
    {
        var test = @"using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Features;

namespace Test;

public interface ITest1
{
}

public class TestImpl1 : ITest1
{
}

public class DefaultFactory
{
    public static IFeatureCollection Shared { get; }
}

public partial class Factory
{
    [KnownFeature(typeof(ITest1), typeof(TestImpl1))]
    [DelegatedFeature(nameof(DefaultFactory.Shared), typeof(DefaultFactory))]
    private partial T Get<T>();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Test;

public partial class Factory
{
    private global::Test.ITest1? _TestImpl1;

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

        if (global::Test.DefaultFactory.Shared is global::DocumentFormat.OpenXml.Features.IFeatureCollection other1 && other1.Get<T>() is T result1)
        {
            return result1;
        }

        return default;
    }
}
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task DelegatedFeaturesField()
    {
        var test = @"using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Features;

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
    private readonly IFeatureCollection _other;

    [KnownFeature(typeof(ITest1), typeof(TestImpl1))]
    [DelegatedFeature(nameof(_other))]
    private partial T Get<T>();
}";
        var created = @"// <auto-generated/>

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Test;

public partial class Factory
{
    private global::Test.ITest1? _TestImpl1;

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

        if (_other is global::DocumentFormat.OpenXml.Features.IFeatureCollection other1 && other1.Get<T>() is T result1)
        {
            return result1;
        }

        return default;
    }
}
";

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { test },
                GeneratedSources =
                {
                    (typeof(DependencyInjectionGenerator), "KnownFeatureAttribute.cs", Attribute),
                    (typeof(DependencyInjectionGenerator), "Factory_Get.cs", created),
                },
            },
        }.RunAsync();
    }
}
