# zezinho's Cultist Circle Planner

Plan what you sacrifice in the Cultist Circle, buy it, and put it in the circle in a few clicks.

Opens from a circle button in the hideout's left toolbar, under the other mod buttons. It only shows once your hideout's Cultist Circle is built.

## Optimizer

- **Aim for**: pick the ritual time you want. The tiers, hours and thresholds are read from the server's live settings, so mods that change them (Cultist Circle Improvements, SVM) are followed.
- **Safety**: Sure thing (95%), Likely (80%) or Cheapest (50%). The ritual multiplies the sacrificed value by a random roll and your hideout skill, so every plan shows the chance of reaching your aim and the chance of each time.
- **Cheapest combination**: finds the cheapest items, from traders and the flea market, that add up to the value you need, using only what the circle accepts and up to the circle's item limit. Each row shows the price and how much of the item's value it is.
- **Three options, skip and ban**: switch between three plans, skip an item for now, ban it for good, or ask for more options.
- **Use junk from my stash** (optional): counts items you already own as free. It never uses found-in-raid items, items your hideout or a started task needs, or items with attachments.
- **Safe flea offers**: ignores flea items with few offers and offers far below the rest.
- **Bonus**: shows which items your hideout upgrades and tasks need, since the bonus reward is drawn from those.
- **Buy and place**: buys with live status per item and a retry, then moves the items into the circle. You press the game's own start button.

## Recipes

Every known direct sacrifice, from the base game, other mods and custom ones: inputs, rewards, ritual time, what is left to buy, the reward's worth and the profit (also per hour). Recipes you can already do from your stash are marked Ready. Search, sort, and pick one at a time. Variants that share inputs are marked, since the game picks one at random.

## Results

Each ritual is recorded when it starts: base value, the random roll, your skill, and the tier or bonus it landed in. While it runs the rewards stay hidden. When it ends you see the rewards, the cost of the sacrifice at today's prices and the profit or loss, plus your last rituals. While a ritual runs or its rewards are still in the circle, the window opens here and buying is locked.

## Excluded items

The circle only takes some items, and a few more are left out on purpose (money, special backpacks, cases, keys and similar). Ban items from the optimizer and allow them again in this tab. The list lives in `circle-exclusions.json` in the server mod folder.

## Settings

Press F12 (BepInEx Configuration Manager):

| Setting | Default |
|---|---|
| Portuguese | off |
| Open key | none (use the toolbar button) |
| Window scale % | 100 (Ctrl + mouse wheel over the window also changes it) |

## Good to know

- Prices come from live trader and flea offers and are estimates. Reward values do not include the flea fee.
- Only rituals started with the mod installed are recorded in Results.
- Ritual history is saved in `circle-history.json` in the server mod folder.
- Not tested with Fika.

## Install

Extract the zip into your game folder. It adds `SPT_Runtime/user/mods/CultistCirclePlanner` (server) and `BepInEx/plugins/CultistCirclePlanner` (client). Both parts are needed.

![Install Gif](https://i.imgur.com/3N6gTe2.gif)

## Português

Planeje o que você sacrifica no Cultist Circle, compre e coloque no círculo com poucos cliques.

Abre por um botão de círculo na barra esquerda do esconderijo, abaixo dos botões de outros mods. Só aparece depois que o Cultist Circle do seu esconderijo estiver construído.

### Otimizador

- **Objetivo**: escolha o tempo do ritual. As faixas, horas e limites vêm das configurações do servidor, então mods que mudam isso (Cultist Circle Improvements, SVM) são acompanhados.
- **Segurança**: Garantido (95%), Provável (80%) ou Mais barato (50%). O ritual multiplica o valor sacrificado por um sorteio e pela sua habilidade de esconderijo, então cada plano mostra a chance de chegar ao objetivo e a chance de cada tempo.
- **Combinação mais barata**: acha os itens mais baratos, de comerciantes e do mercado, que somam o valor necessário, usando só o que o círculo aceita e até o limite de itens do círculo. Cada linha mostra o preço e quanto do valor do item ele representa.
- **Três opções, pular e banir**: alterne entre três planos, pule um item por enquanto, bana de vez ou peça mais opções.
- **Usar itens do estoque** (opcional): conta itens que você já tem como grátis. Nunca usa itens FIR, itens que o esconderijo ou tarefas ativas precisam, nem itens com acessórios.
- **Ofertas seguras**: ignora itens do mercado com poucas ofertas e ofertas bem abaixo das outras.
- **Bônus**: mostra os itens que suas melhorias do esconderijo e tarefas precisam, pois a recompensa bônus sai deles.
- **Comprar e colocar**: compra com status ao vivo por item e nova tentativa, depois move os itens para o círculo. Você aperta o botão de iniciar do jogo.

### Receitas

Todos os sacrifícios diretos conhecidos, do jogo base, de outros mods e personalizados: itens, recompensas, tempo do ritual, o que falta comprar, o valor da recompensa e o lucro (também por hora). Receitas que você já pode fazer com o estoque aparecem como Pronta. Busca, ordenação, e escolha uma por vez. Variantes que usam os mesmos itens são marcadas, pois o jogo sorteia uma.

### Resultados

Cada ritual é registrado quando começa: valor base, o sorteio, sua habilidade e a faixa ou bônus em que caiu. Enquanto ele corre, as recompensas ficam ocultas. Quando termina, você vê as recompensas, o custo do sacrifício com preços de hoje e o lucro ou prejuízo, além dos últimos rituais. Enquanto um ritual corre ou as recompensas ainda estão no círculo, a janela abre aqui e as compras ficam bloqueadas.

### Itens excluídos

O círculo só aceita alguns itens, e outros ficam de fora de propósito (dinheiro, mochilas especiais, maletas, chaves e similares). Bana itens do otimizador e permita de novo nesta aba. A lista fica em `circle-exclusions.json` na pasta do mod no servidor.

### Instalação

Extraia o zip na pasta do jogo. Ele adiciona `SPT_Runtime/user/mods/CultistCirclePlanner` (servidor) e `BepInEx/plugins/CultistCirclePlanner` (cliente). Precisa das duas partes.

## License

MIT

<sub>Enjoying the mod? Curtiu o mod? [Buy me a coffee on Ko-fi](https://ko-fi.com/zezinhoeft)</sub>
