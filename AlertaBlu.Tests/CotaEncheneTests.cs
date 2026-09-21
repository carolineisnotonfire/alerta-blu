using AlertaBlu.Domain;
using Xunit;

namespace AlertaBlu.Tests;

/// <summary>
/// Regression coverage for a bug found in review: a lazily-computed <c>SearchIndex</c> field used
/// to live directly on this record, and since C# synthesizes record equality/hashing over every
/// instance field (mutable ones included), two structurally-identical rows compared unequal once
/// one of them had that field populated by a read. The search index has since moved to the
/// Presentation layer (see <c>MainViewModel</c>'s parallel search-index array); this test locks in
/// that <see cref="CotaEnchente"/> stays a plain immutable record.
/// </summary>
public class CotaEncheneTests
{
    [Fact]
    public void Equals_Should_CompareByValue_Regardless_Of_WhatWasReadFromEitherInstance()
    {
        // Arrange
        var a = new CotaEnchente { Logradouro = "Rua Sao Rafael", Bairro = "Itoupava Norte", CotaMeters = 7.40 };
        var b = new CotaEnchente { Logradouro = "Rua Sao Rafael", Bairro = "Itoupava Norte", CotaMeters = 7.40 };

        // Act: touch every computed property on 'a' only, before comparing — nothing here should
        // mutate hidden state that skews equality.
        _ = a.CotaDisplay;
        _ = a.HasObservacao;

        // Assert
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void DistinctBy_Should_DeduplicateStructurallyIdenticalRows()
    {
        // Arrange
        var a = new CotaEnchente { Logradouro = "Rua Sao Rafael", Bairro = "Itoupava Norte", CotaMeters = 7.40 };
        var b = new CotaEnchente { Logradouro = "Rua Sao Rafael", Bairro = "Itoupava Norte", CotaMeters = 7.40 };

        // Act: force 'a' through a HashSet first, as the old bug depended on read order.
        var seen = new HashSet<CotaEnchente> { a };

        // Assert
        Assert.Contains(b, seen);
    }
}
