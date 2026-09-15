# MKS Subtitle Studio (C# .NET 10 WPF)

Aplikasi desktop modern berkinerja tinggi untuk mengekstrak (*extractor*), melihat (*viewer*), mengedit (*editor*), mengonversi, dan membuat file container **Matroska Subtitles (`.mks` / `.mkv`)** secara native.

---

## 🌟 Fitur Utama

1. **Home Dashboard Terpadu**:
   - **🎬 Modul 1: MKV Subtitle Extractor & Demuxer**: Ekstraksi instan subtitle (SRT, ASS, VTT, PGS) dan font embedded dari file video MKV (single atau batch folder) menggunakan MKVToolNix Engine.
   - **✏️ Modul 2: MKS Subtitle Studio**: Editor dan viewer multi-track `.mks` lengkap dengan virtualized DataGrid, format conversion, time shifting, dan live cinema preview.
2. **Pure C# EBML & Matroska Engine**:
   - Membaca (*Demux*) dan menulis (*Mux*) file `.mks` valid tanpa perlu dependensi biner eksternal yang berat.
   - Parsing VINT (*Variable-Length Integer*), Master Elements, Info, Tracks, Attachments, dan Clusters.
3. **Dukungan Format Subtitle Lengkap**:
   - **SubRip (.srt)**: `S_TEXT/UTF8` dengan formatting tag.
   - **Advanced SubStation Alpha (.ass / .ssa)**: `S_TEXT/ASS` dengan styling, positioning, dan header `CodecPrivate`.
   - **WebVTT (.vtt)**: `S_TEXT/WEBVTT`.
4. **Multi-Track Management**:
   - Menambah trek baru dari file eksternal (`.srt`, `.ass`, `.vtt`).
   - Menghapus, menduplikasi, dan mengubah urutan trek.
   - Mengatur metadata trek: Nama trek, Kode bahasa (ISO-639-2: `ind`, `eng`, `jpn`, dll.), Default flag, Forced flag.
   - Konversi format otomatis (ASS ↔ SRT ↔ VTT).
5. **Interactive Subtitle Editor**:
   - **Virtualized DataGrid** yang sangat responsif untuk ribuan baris subtitle.
   - **Pencarian Real-Time**: Filter cepat daftar baris subtitle langsung di atas tabel.
   - **Formatting Toolbar Cepat**: Tombol pintas untuk Bold, Italic, Underline, `\N` Newline, dan Font Color.
   - Operasi: Tambah baris, Hapus baris, Pecah (*Split*), Pindah urutan (*Move Up/Down*).
6. **Tools Canggih**:
   - **Time Shift Tool**: Geser waktu (+/- milidetik) per baris atau seluruh trek.
   - **FPS Converter**: Konversi sinkronisasi frame rate video (misal 23.976 fps ↔ 25.000 fps).
   - **Find & Replace**: Cari dan ganti teks (mendukung *Match Case* & *Regular Expression*).
   - **Embedded Font Manager**: Lihat, sematkan (*embed*) font TTF/OTF, atau ekstrak font dari container `.mks`.
   - **🌐 Google Translate Online**: Terjemahkan track subtitle instan antar berbagai bahasa dengan proteksi tag/formatting ASS (`{\pos(...)}`, `\N`), SRT, dan HTML utuh 100%.
7. **Visual Cinema Subtitle Preview**:
   - Layar preview 16:9 bersimulasi sinema dengan teks berbayang (*drop shadow*) dan HUD Timecode live.
8. **Portable & Self-Contained Binary**:
   - Bundling otomatis CLI `mkvmerge.exe` dan `mkvextract.exe` langsung di dalam folder aplikasi (`tools/mkvtoolnix/`).

---

## 📁 Struktur Proyek

```
d:\Codingan\C#\mks-subtitle-studio\
├── build_release.ps1                   # Skrip otomatis pembangun rilis mandiri & ZIP
├── build_release.bat                   # Batch wrapper untuk eksekusi 1-klik di Windows
├── dist\                               # Output binary rilis lengkap (Self-Contained Portable)
│   ├── MksStudio-v1.0-win-x64\         # Folder aplikasi siap jalan (tanpa perlu install .NET SDK)
│   └── MksStudio-v1.0-win-x64.zip      # File arsip ZIP siap didistribusikan
│
├── src\
│   ├── MksStudio.Core\                 # Engine Parser EBML, Codec SRT/ASS/VTT, MKVToolNix Wrapper
│   │   ├── Ebml\                       # EbmlReader, EbmlWriter, Vint, EbmlConstants
│   │   ├── Matroska\                   # MatroskaDemuxer, MatroskaMuxer, Models (MksFile, MksTrack, MksAttachment)
│   │   ├── MkvToolNix\                 # MkvToolNixLocator, MkvMergeService, MkvExtractService
│   │   ├── Subtitles\                  # SrtCodec, AssCodec, VttCodec, SubtitleConverter
│   │   └── Operations\                 # TimeShiftService, SearchReplaceService
│   │
│   └── MksStudio.UI\                   # Modern Windows 11 Fluent UI (WPF-UI / MVVM)
│       ├── ViewModels\                 # MainViewModel, MkvExtractorViewModel, TranslationViewModel, dll.
│       ├── Views\                      # MainWindow, TranslationWindow, TimeShiftWindow, SearchReplaceWindow, AttachmentWindow
│       ├── tools\mkvtoolnix\           # Binari bundel mkvmerge.exe & mkvextract.exe
│       └── App.xaml                    # Tema Modern Fluent Dark & Mica Backdrop
│
├── tests\
│   └── MksStudio.Tests\                # 23 Unit Tests xUnit (EBML, Codecs, Translation, Tag Protection, MKVToolNix)
│
└── sample\                             # File Contoh untuk Demo & Pengujian
    ├── demo_multitrack.mks             # File .mks multi-track lengkap dengan embedded font
    ├── sample_indonesian.srt           # Subtitle SRT Bahasa Indonesia
    └── sample_english.ass              # Subtitle ASS Bahasa Inggris dengan styling
```

---

## 🚀 Cara Menjalankan & Membangun Binary

### 1. Menjalankan Mode Development
```powershell
dotnet run --project src/MksStudio.UI
```

### 2. Menjalankan Seluruh Unit Test (23/23 Tests)
```powershell
dotnet test
```

### 3. Membangun Binary Release Lengkap & Portabel
Cukup jalankan file batch atau skrip PowerShell:
```powershell
.\build_release.bat
```
Atau via PowerShell:
```powershell
.\build_release.ps1
```

Hasil kompilasi akan berada di:
* **Setup Installer**: [`dist/MksStudio_Setup_v1.0.exe`](file:///d:/Codingan/C%23/mks-subtitle-studio/dist/MksStudio_Setup_v1.0.exe) (~52 MB)
  - Wizard instalasi modern Windows (Inno Setup 6).
  - Shortcut Desktop & Start Menu.
  - Asosiasi otomatis file `.mks` (klik ganda file `.mks` langsung terbuka di aplikasi).
  - Menu konteks Windows Explorer: Klik kanan file `.mkv` ➔ *"Extract Subtitles with MKS Studio..."*.
  - Uninstaller bersih dan mandiri.
* **File Arsip ZIP Portabel**: [`dist/MksStudio-v1.0-win-x64.zip`](file:///d:/Codingan/C%23/mks-subtitle-studio/dist/MksStudio-v1.0-win-x64.zip) (~79 MB)
* **Folder Aplikasi Portabel**: [`dist/MksStudio-v1.0-win-x64/`](file:///d:/Codingan/C%23/mks-subtitle-studio/dist/MksStudio-v1.0-win-x64)
