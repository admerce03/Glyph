# Third-party notices

Glyph redistributes native and managed components under the licenses summarized below.
This file ships with the application (Help → About Glyph → Third-party notices) and with
MSIX layouts produced by `scripts/publish-msix.ps1`.

Where a vendor ships a longer `NOTICE`/`LICENSE` beside their NuGet package, the publish
script also copies those files next to the package when they are available in the local
NuGet cache (see `artifacts/msix/third-party/`).

Pinned versions: `Directory.Packages.props`.

---

## PDFium (via PDFiumCore / bblanchon.PDFium)

**Use:** PDF rendering and page operations.  
**License:** BSD-3-Clause style copyright + Apache-2.0 (Chromium PDFium).  
**Source:** https://pdfium.googlesource.com/pdfium/

```
Copyright 2014 PDFium Authors. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * Neither the name of Google Inc. nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED.
```

Full Apache-2.0 text: https://www.apache.org/licenses/LICENSE-2.0  
PDFium binaries also incorporate permissive third-party code (FreeType, ICU, zlib, libpng, etc.); see the `LICENSE` file shipped with the PDFium native package.

---

## Magick.NET / ImageMagick

**Use:** Image decode/encode and processing (`Magick.NET-Q16-AnyCPU`).  
**License:** Apache-2.0 (Magick.NET) + ImageMagick license.  
**Sources:** https://github.com/dlemstra/Magick.NET · https://imagemagick.org/license/

```
Copyright Dirk Lemstra https://github.com/dlemstra/Magick.NET.
Licensed under the Apache License, Version 2.0.
```

ImageMagick Studio LLC requires attribution for redistributions that include ImageMagick
software. The Magick.NET NuGet package ships a comprehensive `Notice.txt` covering
ImageMagick and its bundled codecs; that file is copied into `artifacts/msix/third-party/`
when present in the NuGet cache during publish.

---

## PdfPig

**Use:** PDF text extraction / metadata helpers.  
**License:** Apache-2.0  
**Source:** https://github.com/UglyToad/PdfPig  
**Authors:** UglyToad

---

## Microsoft Windows App SDK / WinUI

**Use:** Native Windows UI shell.  
**License:** Microsoft proprietary / Windows SDK terms (NuGet package license).  
**Source:** https://github.com/microsoft/WindowsAppSDK

---

## Microsoft.Extensions.* (DependencyInjection, Logging)

**Use:** App composition and logging.  
**License:** MIT  
**Source:** https://github.com/dotnet/runtime · https://github.com/dotnet/extensions

---

## CommunityToolkit.Mvvm

**Use:** Optional MVVM helpers.  
**License:** MIT  
**Source:** https://github.com/CommunityToolkit/dotnet

---

## Test-only packages (not redistributed in the app package)

| Package | License |
| --- | --- |
| xunit / xunit.runner.visualstudio | Apache-2.0 |
| FluentAssertions 7.x | Apache-2.0 (do not upgrade to 8.x without approval — ADR-011) |
| coverlet.collector | MIT |
| Microsoft.NET.Test.Sdk | proprietary / NuGet terms |

---

## Glyph

Glyph application source is distributed under the repository license. Product name and branding are not third-party marks claimed by the components above.
