import { useRef, useState } from "react";
import { FileSpreadsheet, Loader2, Search } from "lucide-react";
import { catalogService } from "@/services/catalogService";
import { reportingService } from "@/services/reportingService";
import type { SubjectSearchResult } from "@/types/catalog";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const ALLOWED_EXTENSIONS = [".csv", ".xlsx"];

function hasAllowedExtension(fileName: string): boolean {
  const lower = fileName.toLowerCase();
  return ALLOWED_EXTENSIONS.some((ext) => lower.endsWith(ext));
}

export function AdminFeedbackExportPage() {
  const [codeQuery, setCodeQuery] = useState("");
  const [searching, setSearching] = useState(false);
  const [searchResults, setSearchResults] = useState<SubjectSearchResult[]>([]);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [selectedSubject, setSelectedSubject] = useState<SubjectSearchResult | null>(null);
  const [mappingFile, setMappingFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);

  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [exportSuccess, setExportSuccess] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!codeQuery.trim()) return;
    setSearching(true);
    setSearchError(null);
    try {
      const result = await catalogService.searchSubjects({ code: codeQuery.trim(), pageSize: 10 });
      setSearchResults(result.items);
      if (result.items.length === 0) {
        setSearchError("Không tìm thấy môn thi khớp mã đã nhập.");
      }
    } catch {
      setSearchError("Tìm môn thi thất bại.");
    } finally {
      setSearching(false);
    }
  };

  const handleSelectSubject = (subject: SubjectSearchResult) => {
    setSelectedSubject(subject);
    setSearchResults([]);
    setCodeQuery("");
    setExportSuccess(false);
    setExportError(null);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] ?? null;
    setExportSuccess(false);
    setExportError(null);
    if (file && !hasAllowedExtension(file.name)) {
      setFileError("Chỉ chấp nhận file .csv hoặc .xlsx");
      setMappingFile(null);
      return;
    }
    setFileError(null);
    setMappingFile(file);
  };

  const handleExport = async () => {
    if (!selectedSubject || !mappingFile) return;
    setExporting(true);
    setExportError(null);
    setExportSuccess(false);
    try {
      const blob = await reportingService.exportFeedback(selectedSubject.id, mappingFile);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `Feedback_${selectedSubject.subjectCode}.xlsx`);
      document.body.appendChild(link);
      link.click();
      link.parentNode?.removeChild(link);
      window.URL.revokeObjectURL(url);
      setExportSuccess(true);
      setMappingFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
    } catch (err) {
      setExportError(err instanceof Error ? err.message : "Xuất báo cáo thất bại");
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6 p-1">
      <div>
        <h1 className="font-display text-2xl font-bold tracking-tight text-ink flex items-center gap-2">
          <FileSpreadsheet className="h-6 w-6 text-brand-red" />
          Xuất feedback sinh viên
        </h1>
        <p className="text-sm text-ink-soft mt-1">
          Xuất nhận xét/điểm đã công khai (không kèm ghi chú nội bộ) cho từng sinh viên trong 1
          môn thi, kèm định danh thật từ file mapping alias ↔ họ tên/MSSV bạn tải lên.
        </p>
      </div>

      <Card className="shadow-sm border-line">
        <CardHeader className="py-4">
          <CardTitle className="text-sm font-medium">Chọn môn thi</CardTitle>
        </CardHeader>
        <CardContent className="pb-4 space-y-3">
          {selectedSubject ? (
            <div className="flex items-center justify-between gap-3 p-3 border border-brand-red/25 bg-brand-red/5 rounded-lg">
              <div>
                <p className="text-sm font-semibold text-ink">
                  {selectedSubject.subjectCode}
                  {selectedSubject.title ? ` — ${selectedSubject.title}` : ""}
                </p>
                <p className="text-xs text-ink-soft">{selectedSubject.examName}</p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  setSelectedSubject(null);
                  setExportSuccess(false);
                  setExportError(null);
                }}
              >
                Đổi môn
              </Button>
            </div>
          ) : (
            <>
              <form onSubmit={(e) => void handleSearch(e)} className="flex flex-wrap items-end gap-3">
                <div className="flex-1 min-w-[240px] space-y-1.5">
                  <Label htmlFor="subjectCode" className="text-xs">
                    Mã môn thi
                  </Label>
                  <Input
                    id="subjectCode"
                    value={codeQuery}
                    onChange={(e) => setCodeQuery(e.target.value)}
                    placeholder="vd: PMG201c"
                  />
                </div>
                <Button type="submit" disabled={searching || !codeQuery.trim()} className="h-9">
                  {searching ? <Loader2 className="h-4 w-4 animate-spin" /> : <Search className="h-4 w-4" />}
                </Button>
              </form>
              {searchError && <p className="text-sm text-destructive">{searchError}</p>}
              {searchResults.length > 0 && (
                <ul className="space-y-1.5">
                  {searchResults.map((s) => (
                    <li key={s.id}>
                      <button
                        type="button"
                        onClick={() => handleSelectSubject(s)}
                        className="w-full text-left px-3 py-2 rounded-lg border border-line hover:border-brand-red/30 hover:bg-secondary text-sm"
                      >
                        <span className="font-medium text-ink">{s.subjectCode}</span>
                        {s.title ? <span className="text-ink-soft"> — {s.title}</span> : null}
                        <span className="text-xs text-ink-soft ml-2">{s.examName}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {selectedSubject && (
        <Card className="shadow-sm border-line">
          <CardHeader className="py-4">
            <CardTitle className="text-sm font-medium">File mapping alias ↔ sinh viên</CardTitle>
          </CardHeader>
          <CardContent className="pb-4 space-y-4">
            <div>
              <Label className="text-xs mb-1 block">
                File .csv hoặc .xlsx, bắt buộc cột: AliasNumber, StudentCode, StudentName
              </Label>
              <input
                ref={fileInputRef}
                type="file"
                accept=".csv,.xlsx"
                onChange={handleFileChange}
                className="block w-full text-sm text-ink file:mr-3 file:px-3 file:py-1.5 file:rounded-lg file:border file:border-line file:bg-secondary file:text-sm file:font-medium file:text-ink hover:file:bg-secondary/80"
              />
              {fileError && <p className="text-sm text-destructive mt-1.5">{fileError}</p>}
            </div>

            {exportError && <p className="text-sm text-destructive">{exportError}</p>}
            {exportSuccess && (
              <p className="text-sm text-done">Xuất báo cáo thành công, đã tải file về máy.</p>
            )}

            <Button
              type="button"
              onClick={() => void handleExport()}
              disabled={exporting || !mappingFile}
              className="bg-brand-red text-white hover:bg-brand-red/90"
            >
              {exporting ? (
                <span className="flex items-center gap-2">
                  <Loader2 className="h-4 w-4 animate-spin" /> Đang xuất...
                </span>
              ) : (
                "Xuất báo cáo Excel"
              )}
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
