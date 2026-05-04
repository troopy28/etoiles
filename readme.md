# Gamejam Espace

Jeu de simulation spatiale : pilotage d'un vaisseau dans une galaxie procédurale avec gravité multi-corps, carburant, et navigation automatique.

## Équipe

- NewYorkiki
- Asphox
- troopy28
- yoyoazs

## Jouer au jeu

### Télécharger le jeu

Méthode simple : télécharger l'exécutable zippé du jeu [sur ce lien](https://github.com/troopy28/etoiles/releases/download/1.0/builds.zip).

Pour le compiler vous-même, se référer à la section "Build" plus bas.

### Commandes et gameplay

| Action | Contrôle |
|---|---|
| Propulsion avant/arrière | `Z` / `S` |
| Propulsion latérale | `Q` / `D` |
| Propulsion verticale | `R` / `F` |
| Freinage | `B` |
| Boost | `Shift` (note : peut s'appliquer au freinage)|
| Tangage et roulis | Souris |
| Lacet | `A` / `E` |
| Regard libre | Clic molette |
| "Autopilote" : assistante à mise en orbite | `Espace` |
| Pause | `Échap` |

Il est possible de **cibler un corps stellaire** (étoile, planète...) en le pointant et en cliquant dessus. Des informations sur l'objet seront alors affichées, et le freinage sera fait tel que le vaisseau sera à l'arrêt dans le référentiel de l'objet.

La **poussée consomme du carburant**. Le ravitaillement se fait passivement par proximité des étoiles de classes O, B, A ou F (les plus chaudes) : il suffit de s'en approcher. Les étoiles froides (G, K, M...) ne rechargent pas.

Un **radar** en bas à gauche indique la direction et la distance de la cible sélectionnée.

## Build

### Prérequis

- **Unity 6000.4.1f1**

### Ouvrir le projet

Ouvrir le dossier `GameJam Espace/` dans Unity Hub. Scène de démarrage : `Assets/Assets/Level/Scenes/MainMenu.unity`.

### Générer l'exécutable

- **Windows** : *File > Build Profiles > Windows x86_64*
- **Linux** : *File > Build Profiles > Linux x86_64*

## Simulation de gravité

### Le problème des grands mondes

float32 perd en précision vers ~50k unités. Les déplacements orbitaux deviennent plus petits que l'epsilon : les choses tremblement, dérivent,etc. L'univers étant encore plus grand que ça (de beaucoup), on doit utiliser des stratégies un peu moins habituelles.

**Double précision dans la sim.** Positions et masses en `double4` (xyz, w = masse), calcul et intégration entièrement en double. Le cast double->float n'a lieu que dans l'`IJobParallelForTransform` qui écrit les Transforms, Unity ne supportant pas mieux. Et d'ailleurs, ça pose problème, d'où le paragraphe suivant.

**Floating origin.** Même en faisant la simulation en double précision, il faut revenir dans Unity, qui supporte uniquement les float : les Transforms, le rendu, le physics Unity restent en float32 et tremblent autant qu'avant si les coordonnées sont grandes. La seule solution est donc de garder le vaisseau près de l'origine en déplaçant tout l'univers autour de lui pour maintenir l'illusion. Quand il dépasse 500 unités, on translate tous les corps pour le ramener à (0,0,0). Comme `curr` et `prev` bougent ensemble, la vélocité implicite `(curr - prev) / dt` est préservée.

### Intégrateur

`IJobParallelFor` Burst à chaque `FixedUpdate`, Verlet : `new_pos = 2*curr - prev + acc*G*dt²`. Chaque corps itère sur tous les autres, O(n²), boucle déroulée x4 (`acc0..acc3`) à la main car Burst est apparemment assez mauvais en unrolling. Softening `dist² + 1e-20` pour éviter le branch sur l'auto-interaction. La poussée du vaisseau arrive comme accélération externe `float3` dans le même pas.

## Stack

- URP 17.4
- Input System 1.19
- Jobs System + Mathematics (simulation gravité, voir ci-dessus)
- Timeline (cinématique hangar)
- Note : des outils IA ont été utilisés à toutes les étapes du développement, notamment
    - Pour la génération de code
    - Pour générer le modèle 3D du vaisseau
    - Pour générer les sons et la musique
    - En conséquence, ce mini-jeu ne dépend d'aucun asset externe soumis à une licence
