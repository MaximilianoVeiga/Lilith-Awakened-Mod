# Lilith Awakened Mod 0.1.1 RC4

[Home (English)](README.md) · [繁體中文](README_繁體中文.md) · [简体中文](README_简体中文.md) · [日本語](README_日本語.md) · [English](README_EN.md) · **Português (BR)**

Este é um MOD comunitário não oficial para o jogo de companhia de desktop *The NOexistenceN of Lilith*. Ele adiciona conversas com IA, entrada por texto e por voz, saída de voz em chinês e japonês, falas para diálogos que antes não tinham dublagem, recursos de clima e controles locais do computador previamente revisados.

## Atualização RC4

* Compatibilidade com a interface de configurações por categorias da Build oficial `24273498`.
* Os controles do MOD agora aparecem apenas na aba **Controles** e não se sobrepõem mais a outras páginas de configuração.
* Corrigidos o F6/F7 que funcionava apenas uma vez, o remapeamento de teclas e o cancelamento com Esc.
* A seleção de vozes nativas e suplementares em chinês e japonês continua totalmente separada.
* Adicionados o chat e o reconhecimento de fala do Qwen já testados, a busca na web nativa do provedor e o roteamento local confiável para comandos explícitos de computador.
* Os aplicativos instalados agora podem ser identificados a partir de processos em execução, apps do Menu Iniciar do Windows, registros da Store/MSIX e atalhos, sem caminhos fixos do jogador.
* Corrigido o problema em que as janelas de chave de API retinham o foco do mouse e do teclado, e a janela nativa de resgate de códigos foi mantida separada da inserção da chave de API.
* O instalador pode, opcionalmente, mesclar a ponte Lilith Codex com privacidade minimizada aos Codex Hooks existentes, sem substituir hooks não relacionados.

## Compatibilidade de provedores de IA

**O Gemini continua sendo o provedor recomendado e mais amplamente otimizado na versão 0.1.1-RC4.** O comportamento da personagem, o tratamento de contexto, a saída multilíngue, a separação entre texto exibido e voz em japonês, o embasamento via Google Search, as cartas da IA e as ferramentas de computador em linguagem natural foram desenvolvidos principalmente em torno da API do Gemini.

O Qwen agora conta com uma integração testada, usando `qwen3.7-plus` para conversas e `qwen3-asr-flash` para reconhecimento de fala. Ele oferece busca na web nativa do provedor para informações atuais e roteamento local determinístico para comandos explícitos de aplicativos, mídia, capturas de tela e controles de computador revisados. A disponibilidade, a cota gratuita e as exigências de cobrança dependem da conta do jogador no Alibaba Cloud Model Studio e da região.

Atualmente, OpenAI e DeepSeek utilizam uma **camada experimental de compatibilidade para chat de texto**. As requisições básicas estão implementadas, e o GPT-SoVITS local pode narrar uma resposta retornada com sucesso, mas esses provedores não foram testados de ponta a ponta em seus modelos disponíveis, formatos de resposta, cotas, restrições regionais ou mudanças futuras de API. A busca na web nativa do provedor e a chamada de funções não estão integradas para eles; apenas alguns comandos locais explícitos ainda podem ser reconhecidos pelo próprio MOD.

O reconhecimento de fala do `F6` segue o provedor selecionado no caso do Gemini e do Qwen. OpenAI e DeepSeek usam atualmente a transcrição do Gemini e, por isso, ainda exigem uma chave de API do Gemini salva separadamente para a entrada de voz.

## Instalação com um clique

1. Execute `LilithAI-Mod-Setup.exe`.
2. O instalador localizará o jogo automaticamente usando as informações do registro do Steam e todas as pastas da Biblioteca Steam. Se o jogo não for encontrado, clique em **Procurar** e selecione a pasta que contém `Lilith.exe`.
3. Mantenha os componentes necessários selecionados e clique em **Instalar / Atualizar**.
4. A primeira instalação da geração dinâmica de voz por IA baixará um ambiente de inferência separado. Isso pode levar alguns minutos. Nenhuma instalação de Python já presente no computador do jogador será usada ou modificada.
5. Depois de iniciar o jogo, clique com o botão direito no ícone da Lilith no canto inferior esquerdo, selecione **Adicionar chave de API** e um provedor de modelo, e informe a chave de API do próprio jogador.
6. Se a ponte opcional do Codex tiver sido selecionada, o Codex poderá pedir que você revise e ative os três hooks da Lilith na próxima inicialização. O instalador os mescla aos hooks existentes em vez de substituir entradas não relacionadas.

O instalador não inclui a chave de API do autor, histórico de conversas, nome do jogador, logs, capturas de tela, conjuntos de dados de treinamento ou caminhos do computador de desenvolvimento.

As atualizações não sobrescrevem chaves de API existentes, memória de conversas, atalhos de teclado ou configurações do jogador.

## Controles

* `F7` (padrão): abre o balão de entrada de texto.
* Segurar `F6` (padrão): grava a entrada de voz. Solte a tecla para transcrever e enviar.
* Ambas as teclas podem ser remapeadas nas configurações do jogo.
* A saída de voz do jogo pode alternar entre chinês e japonês. O idioma escolhido será lembrado na próxima vez que o jogo for iniciado.
* Os **Controles avançados do computador** vêm desativados por padrão. Somente depois que o jogador ativar essa opção a IA poderá usar funções aprovadas da lista de permissões, como abrir aplicativos, controles de mídia, controles de janela, capturas de tela, temporizadores, bloquear o computador e o modo de suspensão.

## Requisitos de hardware e rede

* MOD principal: Windows 10/11 x64, sem requisitos adicionais de hardware.
* Conversas com IA e reconhecimento de fala: exigem conexão com a internet e a chave de API do próprio jogador.
* Geração dinâmica de voz por IA: recomenda-se uma placa de vídeo NVIDIA, preferencialmente com pelo menos 8 GB de VRAM. Se não houver uma placa NVIDIA compatível, a versão para CPU será instalada. Recomendam-se pelo menos 16 GB de RAM, e a geração de voz será mais lenta.
* O pacote completo de falas suplementares tem aproximadamente 301 MB.
* O pacote do modelo de voz dinâmica tem aproximadamente 1,98 GB. É necessário espaço em disco adicional durante a instalação para criar o ambiente de inferência.

## Privacidade

* O texto das conversas e as gravações do reconhecimento de fala são enviados ao provedor de IA escolhido pelo jogador e ficam sujeitos aos termos de serviço e à política de privacidade desse provedor.
* A síntese de voz GPT-SoVITS é executada localmente em `127.0.0.1` e não aceita conexões de rede externas.
* A localização automática do clima usa a cidade aproximada derivada do endereço IP público do jogador. O MOD não armazena o endereço IP.
* As capturas de tela são salvas apenas na pasta `Imagens\Lilith Screenshots` do jogador e não são enviadas automaticamente a nenhum modelo.
* O MOD não oferece acesso a comandos irrestritos do PowerShell ou do CMD, exclusão de arquivos, obtenção de senhas ou ao conteúdo da área de transferência.

## Atualizar, reparar e remover

* Execute o mesmo instalador, ou uma versão mais recente, e selecione **Instalar / Atualizar** para reparar arquivos ausentes ou atualizar o MOD.
* **Remover MOD** exclui apenas os arquivos gerenciados por este MOD. Chaves de API, memória de conversas e configurações são preservadas por padrão.
* Para remover completamente os dados pessoais, apague manualmente os seguintes arquivos após a desinstalação:

  * `BepInEx\config\community.lilith.textinjector.cfg`
  * `BepInEx\data\LilithTextInjector\memory.json`
  * `BepInEx\data\LilithTextInjector\ai-note-state.json`

## Solução de problemas

* Log do MOD: `BepInEx\LogOutput.log`
* Log do host de voz: `BepInEx\data\LilithTextInjector\voice-runtime\logs\voice-host.log`
* Log da instalação de voz: `BepInEx\data\LilithTextInjector\voice-runtime\voice-runtime-install.log`
* Se o MOD parar de carregar após uma atualização do jogo no Steam, execute o instalador novamente antes de mais nada. Se o problema continuar, inclua os logs relevantes ao relatar a ocorrência.

## Aviso de distribuição

### Isenção de responsabilidade do MOD de voz por IA não oficial

Este MOD é uma modificação de jogo não oficial e não comercial, criada de forma independente por um jogador. Ele é disponibilizado exclusivamente para uso comunitário relacionado ao jogo e para entretenimento pessoal.

Este MOD não é conteúdo oficial do jogo e não possui vínculo, operação, autorização, patrocínio, endosso ou qualquer outra associação com a desenvolvedora do jogo, a publicadora, os detentores de direitos das personagens, os produtores fonográficos, os dubladores originais, Google, OpenAI, DeepSeek, BepInEx, GPT-SoVITS ou qualquer outro fornecedor de tecnologia, software ou serviço relacionado.

Este MOD não representa as opiniões ou posições de nenhuma das pessoas, organizações ou empresas listadas acima.

Os diálogos suplementares e o conteúdo de voz adicional incluídos neste MOD foram gerados ou sintetizados por meio de tecnologia de inteligência artificial e não foram regravados pelos dubladores originais.

Não deturpe, edite, redistribua nem divulgue tal conteúdo como declarações feitas pelos dubladores originais, gravações oficiais, conteúdo adicional oficial ou qualquer forma de obra oficialmente autorizada.

O áudio de diálogos suplementares, os modelos de voz e os resultados gerados utilizados por este MOD podem envolver uso derivado do áudio original do jogo, de características vocais das personagens, de traços de interpretação ou de outros materiais relacionados.

Esta isenção de responsabilidade serve apenas para explicar a origem, o método de produção e a natureza não oficial do conteúdo. Ela não concede nenhuma licença ou autorização e não indica que qualquer detentor de direitos tenha aprovado, endossado ou permitido tal uso.

Todos os direitos relativos ao título do jogo, aos nomes das personagens, ao design das personagens, aos diálogos originais, ao áudio, às interpretações e a outros materiais relacionados pertencem aos seus respectivos detentores legais.

O criador deste MOD não reivindica a titularidade de nenhum direito autoral, marca registrada, direito de intérprete, direito de produtor fonográfico ou outro direito patrimonial relativo à obra original.

Este MOD é fornecido gratuitamente. Ele não pode ser vendido, redistribuído mediante pagamento, distribuído atrás de um paywall, nem usado para publicidade comercial, endossos de voz, promoção política, fraude, falsidade ideológica, ofensas, difamação, conteúdo adulto ou qualquer outra finalidade que possa prejudicar os direitos e interesses legais dos dubladores originais ou de outros detentores de direitos.

Sem permissão, os usuários não podem extrair, revender, retreinar ou redistribuir qualquer conteúdo de voz gerado por IA ou modelo de voz incluído neste MOD.

Tal conteúdo não pode ser usado para se passar pelos dubladores originais nem para criar material que possa causar confusão, mal-entendido ou deturpação junto ao público.

Os usuários são responsáveis por cumprir as leis e os regulamentos aplicáveis em sua jurisdição, bem como o contrato de usuário do jogo, as políticas de MOD e as regras da plataforma de distribuição.

Se algum detentor de direitos considerar que este MOD afeta ou infringe seus direitos ou interesses, poderá entrar em contato com o criador pelo seguinte canal:

Contato: **[mimimi5206666@gmail.com]**

Ao receber uma notificação específica e verificável relacionada a direitos, o criador analisará prontamente a questão e, quando apropriado, suspenderá a distribuição, removerá ou modificará o conteúdo em questão.

**Publicado por:** MIMI  
**Versão:** 0.1.1-RC4  
**Data de lançamento:** 18 de julho de 2026
