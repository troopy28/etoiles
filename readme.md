# Gamejam Espace

Jeu de simulation spatiale : pilotage d'un vaisseau dans une galaxie procédurale avec gravité multi-corps, carburant, et navigation automatique.

## Commandes

| Action | Contrôle |
|---|---|
| Propulsion avant/arrière | `Z` / `S` |
| Propulsion latérale | `Q` / `D` |
| Propulsion verticale | `R` / `F` |
| Freinage | `B` |
| Boost | `Shift` |
| Tangage | Souris Y |
| Roulis | Souris X |
| Lacet | `A` / `E` |
| Regard libre | Clic molette |
| Autopilote | `Espace` |
| Pause | `Échap` |

## Prérequis

- **Unity 6000.4.1f1**
- Module **Linux Build Support (Mono)** pour cibler Linux

## Ouvrir le projet

Ouvrir le dossier `GameJam Espace/` dans Unity Hub. Scène de démarrage : `Assets/Assets/Level/Scenes/MainMenu.unity`.

## Build

- **Windows** : *File → Build Settings → Windows x86_64*
- **Linux** : *File → Build Settings → Linux x86_64*

## Stack

- URP 17.4
- Input System 1.19
- Jobs System + Mathematics — simulation N-body : chaque étoile et planète exerce une attraction gravitationnelle sur le vaisseau en temps réel
- Timeline (cinématique hangar)
