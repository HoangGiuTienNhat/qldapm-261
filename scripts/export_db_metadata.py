"""Read schema catalogs from this project's running database; never migrate/seed/reset."""
import json
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]

def main():
    sql=(ROOT/'scripts/db_schema_metadata.sql').read_text(encoding='utf-8')
    result=subprocess.run(['docker','compose','-f',str(ROOT/'compose.yml'),'-p','restaurant_ordering','exec','-T','postgres','sh','-c',
        'exec psql -X -q -A -t -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1'],
        input=sql,encoding='utf-8',capture_output=True,cwd=ROOT,check=True)
    metadata=json.loads(result.stdout)
    (ROOT/'docs/db').mkdir(parents=True,exist_ok=True)
    (ROOT/'docs/db/schema-metadata.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'Exported schema only: {len(metadata["tables"])} tables. No application data read or changed.')

if __name__=='__main__':main()
