using System;
using System.Threading.Tasks;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.Properties;

/// <summary>
/// <see cref="ImageBufferView.MaxDecodeConcurrency"/> 静态属性的单元测试。
/// </summary>
public sealed class MaxDecodeConcurrencyTests : IDisposable
{
    private readonly int _originalValue;

    /// <summary>
    /// 记录测试前的原始值，测试后恢复。
    /// </summary>
    public MaxDecodeConcurrencyTests()
    {
        _originalValue = ImageBufferView.MaxDecodeConcurrency;
    }

    /// <summary>
    /// 测试后恢复原始并发值，避免污染其他测试。
    /// </summary>
    public void Dispose()
    {
        ImageBufferView.MaxDecodeConcurrency = _originalValue;
    }

    /// <summary>
    /// 默认值应等于 <see cref="Environment.ProcessorCount"/>。
    /// </summary>
    [Fact]
    public void DefaultValue_EqualsProcessorCount()
    {
        Assert.Equal(Environment.ProcessorCount, _originalValue);
    }

    /// <summary>
    /// 设置为合法正整数值应能通过 getter 读取到相同的值。
    /// </summary>
    [Fact]
    public void SetValidValue_ReturnsSameValue()
    {
        ImageBufferView.MaxDecodeConcurrency = 4;
        Assert.Equal(4, ImageBufferView.MaxDecodeConcurrency);
    }

    /// <summary>
    /// 设置为 1 应正常工作。
    /// </summary>
    [Fact]
    public void SetToOne_Works()
    {
        ImageBufferView.MaxDecodeConcurrency = 1;
        Assert.Equal(1, ImageBufferView.MaxDecodeConcurrency);
    }

    /// <summary>
    /// 设置大值应正常工作。
    /// </summary>
    [Fact]
    public void SetLargeValue_Works()
    {
        ImageBufferView.MaxDecodeConcurrency = 128;
        Assert.Equal(128, ImageBufferView.MaxDecodeConcurrency);
    }

    /// <summary>
    /// 设置为零应抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    [Fact]
    public void SetZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageBufferView.MaxDecodeConcurrency = 0);
    }

    /// <summary>
    /// 设置为负值应抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    [Fact]
    public void SetNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageBufferView.MaxDecodeConcurrency = -1);
    }

    /// <summary>
    /// 多线程并发设置不应崩溃。
    /// </summary>
    [Fact]
    public void ConcurrentSet_DoesNotCrash()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        Parallel.For(0, 16, i =>
        {
            try
            {
                ImageBufferView.MaxDecodeConcurrency = (i % 8) + 1;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.Empty(exceptions);
    }
}
