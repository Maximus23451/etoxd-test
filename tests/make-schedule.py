"""Create a real XLSX fixture with native Excel numeric date/time cells."""
from datetime import date
from pathlib import Path
import sys
import zipfile
from xml.sax.saxutils import escape

sheet_ns = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
rel_ns = 'http://schemas.openxmlformats.org/package/2006/relationships'
with zipfile.ZipFile(Path(sys.argv[1]), 'w', zipfile.ZIP_DEFLATED) as book:
    book.writestr('[Content_Types].xml', '''<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>''')
    book.writestr('_rels/.rels', f'<Relationships xmlns="{rel_ns}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>')
    book.writestr('xl/workbook.xml', f'<workbook xmlns="{sheet_ns}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Schedule" sheetId="1" r:id="rId1"/></sheets></workbook>')
    book.writestr('xl/_rels/workbook.xml.rels', f'<Relationships xmlns="{rel_ns}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>')
    book.writestr('xl/styles.xml', f'''<styleSheet xmlns="{sheet_ns}"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="20" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>''')
    rows = [['Dátum', 'Kezdés', 'Vége', 'Csapat', 'Edzés', 'Helyszín', 'Edző'], ['2030-01-02', '16:00', '17:30', 'U15', 'Imported XLSX', 'Test pitch', 'Test Coach']]
    xml = f'<worksheet xmlns="{sheet_ns}"><sheetData>'
    serial = (date(2030, 1, 2) - date(1899, 12, 30)).days
    for i, row in enumerate(rows, 1):
        xml += f'<row r="{i}">'
        for j, value in enumerate(row):
            address = f'{chr(65+j)}{i}'
            if i == 2 and j <= 2:
                number = [serial, 16 / 24, 17.5 / 24][j]
                style = 1 if j == 0 else 2
                xml += f'<c r="{address}" s="{style}"><v>{number}</v></c>'
            else:
                xml += f'<c r="{address}" t="inlineStr"><is><t>{escape(value)}</t></is></c>'
        xml += '</row>'
    book.writestr('xl/worksheets/sheet1.xml', xml + '</sheetData></worksheet>')
