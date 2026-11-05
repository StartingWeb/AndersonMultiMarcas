-- Synthetic data only; run through scripts/dev/local_sql.py against its disposable container.
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM Loja)
BEGIN
INSERT Loja (Nome,RazaoSocial,Cnpj,Email,Telefone,Endereco,Numero,Bairro,Cidade,Uf,Cep,DataCadastro,Ativo)
VALUES (N'Loja sintetica validacao',N'Empresa sintetica',NULL,N'loja@example.invalid',N'16999990000',N'Rua de teste',N'100',N'Centro',N'Taquaritinga',N'SP',N'15900000',SYSUTCDATETIME(),1);
INSERT Marca (Nome,DataCadastro,Ativo) VALUES (N'Marca Teste',SYSUTCDATETIME(),1);
INSERT Vendedor (LojaId,Nome,Email,Telefone,Whatsapp,DataCadastro,Ativo)
VALUES ((SELECT MIN(Id) FROM Loja),N'Vendedor sintetico',N'vendedor@example.invalid',N'16999990000',N'16999990000',SYSUTCDATETIME(),1);
WITH Numbers AS (SELECT 1 n UNION ALL SELECT n+1 FROM Numbers WHERE n<42)
INSERT Veiculo (LojaId,MarcaId,Titulo,Modelo,AnoFabricacao,AnoModelo,Cor,Combustivel,Cambio,Quilometragem,PrecoVenda,AceitaTroca,Financiavel,Destaque,Seminovo,Vendido,Descricao,ImportadoMidia,MotoEletrica,DataCadastro,Ativo,QuantidadeCliques,QuantidadeVisualizacoes)
SELECT (SELECT MIN(Id) FROM Loja),(SELECT MIN(Id) FROM Marca),CONCAT(N'Veiculo sintetico ',n),CONCAT(N'Modelo ',n%4),2025,2026,N'Branco',N'Flex',N'Automatico',CASE WHEN n%3=0 THEN 0 ELSE 10000 END,30000+n*1000,1,1,CASE WHEN n%5=0 THEN 1 ELSE 0 END,CASE WHEN n%3=0 THEN 0 ELSE 1 END,CASE WHEN n=42 THEN 1 ELSE 0 END,N'Dados sinteticos de validacao',0,0,SYSUTCDATETIME(),CASE WHEN n=41 THEN 0 ELSE 1 END,0,0 FROM Numbers;
INSERT VeiculoMidia (VeiculoId,NomeArquivo,Url,Tipo,Capa,Ordem,DataCadastro,Ativo)
SELECT Id,N'carroDefault.png',N'/img/carroDefault.png',N'Imagem',1,0,SYSUTCDATETIME(),1 FROM Veiculo;
END;
SELECT COUNT(*) AS SyntheticVehicles FROM Veiculo;
